using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class EmployeeSeeder
{
    // Embedded name pools so the generated data looks plausible without
    // pulling in a library like Bogus. Emails include an index suffix to
    // guarantee uniqueness even when a name combo repeats.
    private static readonly string[] FirstNames =
    [
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda",
        "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica",
        "Thomas", "Sarah", "Charles", "Karen", "Christopher", "Lisa", "Daniel", "Nancy",
        "Matthew", "Betty", "Anthony", "Sandra", "Mark", "Margaret", "Donald", "Ashley",
        "Steven", "Kimberly", "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle",
        "Kenneth", "Carol", "Kevin", "Amanda", "Brian", "Dorothy", "George", "Melissa",
        "Timothy", "Deborah", "Ronald", "Stephanie", "Edward", "Rebecca", "Jeffrey", "Sharon",
        "Ryan", "Cynthia", "Jacob", "Kathleen", "Gary", "Amy", "Nicholas", "Shirley",
        "Eric", "Angela", "Jonathan", "Helen", "Stephen", "Anna", "Larry", "Brenda",
        "Justin", "Pamela", "Scott", "Nicole", "Brandon", "Emma", "Benjamin", "Samantha",
        "Samuel", "Katherine", "Gregory", "Christine", "Alexander", "Debra", "Frank", "Rachel",
        "Patrick", "Catherine", "Raymond", "Carolyn", "Jack", "Janet", "Dennis", "Ruth",
        "Jerry", "Maria", "Tyler", "Heather", "Aaron", "Diane", "Jose", "Virginia",
        "Adam", "Julie", "Nathan", "Joyce", "Henry", "Victoria", "Douglas", "Olivia",
        "Zachary", "Kelly", "Peter", "Christina", "Kyle", "Lauren", "Walter", "Joan",
        "Ethan", "Evelyn", "Jeremy", "Judith", "Harold", "Megan", "Keith", "Cheryl",
        "Christian", "Andrea", "Roger", "Hannah", "Noah", "Martha", "Gerald", "Jacqueline",
        "Carl", "Frances", "Terry", "Gloria", "Sean", "Ann", "Austin", "Teresa",
        "Arthur", "Kathryn", "Lawrence", "Sara", "Jesse", "Janice"
    ];

    private static readonly string[] LastNames =
    [
        "Anderson", "Baker", "Chen", "Davis", "Evans", "Fischer", "Garcia", "Hughes",
        "Ivanov", "Johnson", "Khan", "Lopez", "Martin", "Nguyen", "O'Brien", "Patel",
        "Quinn", "Rossi", "Silva", "Tanaka", "Ulrich", "Vargas", "Wang", "Xu",
        "Young", "Zhang", "Ahmed", "Brown", "Cohen", "Dubois", "Eriksson", "Ferrari",
        "Gupta", "Hansen", "Ibrahim", "Jensen", "Kim", "Lindqvist", "Muller", "Novak",
    ];

    private static readonly (string Country, string Currency)[] Locations =
    [
        ("US", "USD"),
        ("UK", "GBP"),
        ("Germany", "EUR"),
        ("France", "EUR"),
        ("Spain", "EUR"),
        ("Netherlands", "EUR"),
        ("Canada", "CAD"),
        ("Australia", "AUD"),
        ("India", "INR"),
        ("Brazil", "BRL"),
        ("Japan", "JPY"),
        ("Singapore", "SGD"),
    ];

    private static readonly string[] Departments =
    [
        "Engineering", "Product", "Sales", "Marketing",
        "Finance", "HR", "Operations", "Legal", "Support",
    ];

    private static readonly string[] JobTitles =
    [
        "Engineer", "Senior Engineer", "Staff Engineer", "Manager",
        "Senior Manager", "Director", "Analyst", "Specialist", "Coordinator",
        "Lead", "Consultant", "Associate",
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var targetCount = config.GetValue("Seed:EmployeeCount", 10_000);
        if (targetCount <= 0)
        {
            logger.LogInformation("Seed:EmployeeCount <= 0 — skipping employee seed.");
            return;
        }

        var existing = await db.Employees.CountAsync(ct);
        if (existing >= targetCount)
        {
            logger.LogInformation("Employees already seeded ({Count}).", existing);
            return;
        }

        var toCreate = targetCount - existing;

        // Fixed seed so re-seeding on a fresh DB produces identical fixtures.
        // Useful for demos and for anyone reviewing the output.
        var random = new Random(20261014);

        // Insert in batches. Without Clear() between batches, EF keeps all
        // 10k entities tracked in memory and the insert slows to a crawl.
        const int batchSize = 1_000;
        var total = 0;

        while (total < toCreate)
        {
            var thisBatch = Math.Min(batchSize, toCreate - total);
            var batch = new List<Employee>(thisBatch);

            for (var i = 0; i < thisBatch; i++)
            {
                var index = existing + total + i;
                batch.Add(GenerateEmployee(random, index));
            }

            db.Employees.AddRange(batch);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            total += thisBatch;
        }

        logger.LogInformation("Seeded {Count} employees.", toCreate);
    }

    private static Employee GenerateEmployee(Random random, int index)
    {
        var firstName = FirstNames[random.Next(FirstNames.Length)];
        var lastName = LastNames[random.Next(LastNames.Length)];
        var (country, currency) = Locations[random.Next(Locations.Length)];
        var department = Departments[random.Next(Departments.Length)];
        var jobTitle = JobTitles[random.Next(JobTitles.Length)];

        // Hire date spread over the last 5 years.
        var daysAgo = random.Next(0, 365 * 5);
        var hireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));

        // Salary range in local currency. Roughly 50k–200k — deliberately
        // not currency-converted; the app treats each salary as local.
        var baseSalary = random.Next(50_000, 200_001);
        var bonuses = random.Next(0, (int)(baseSalary * 0.20m));

        // ~5% are former employees.
        var isActive = random.Next(100) >= 5;

        DateOnly? terminationDate = null;
        if (!isActive)
        {
            var minDaysAfterHire = 30;
            var maxDaysAfterHire = Math.Max(minDaysAfterHire, (DateTime.UtcNow - hireDate.ToDateTime(TimeOnly.MinValue)).Days);
            var daysAfterHire = random.Next(minDaysAfterHire, maxDaysAfterHire);
            terminationDate = hireDate.AddDays(daysAfterHire);
        }

        return new Employee
        {
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant().Replace("'", "")}.{index}@acme.org",
            JobTitle = jobTitle,
            Department = department,
            Country = country,
            Currency = currency,
            BaseSalary = baseSalary,
            Bonuses = bonuses,
            HireDate = hireDate,
            IsActive = isActive,
            TerminationDate = terminationDate,
        };
    }
}