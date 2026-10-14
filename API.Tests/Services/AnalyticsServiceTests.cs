using API.Data;
using API.Entities;
using API.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Tests.Services;

public class AnalyticsServiceTests
{
    private static readonly DateOnly HireDate = new(2023, 1, 1);

    private static (AnalyticsService Sut, AppDbContext Db) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        return (new AnalyticsService(db), db);
    }

    private static Employee Emp(
        string department,
        string country,
        string currency,
        decimal baseSalary,
        decimal bonuses = 0m,
        bool isActive = true) => new()
        {
            FirstName = "F",
            LastName = "L",
            Email = Guid.NewGuid().ToString("N") + "@acme.org",
            JobTitle = "Engineer",
            Department = department,
            Country = country,
            Currency = currency,
            BaseSalary = baseSalary,
            Bonuses = bonuses,
            HireDate = HireDate,
            IsActive = isActive,
            TerminationDate = isActive ? null : new DateOnly(2024, 1, 1),
        };

    // ---- headcount -----------------------------------------------------

    [Fact]
    public async Task GetHeadcount_GroupsByDepartment_OrderedDescending()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Engineering", "US", "USD", 100_000m),
            Emp("Engineering", "US", "USD", 100_000m),
            Emp("Engineering", "US", "USD", 100_000m),
            Emp("Sales", "US", "USD", 90_000m),
            Emp("HR", "US", "USD", 80_000m));
        await db.SaveChangesAsync();

        var result = await sut.GetHeadcountByDepartmentAsync(includeInactive: false, default);

        Assert.Equal(3, result.Count);
        Assert.Equal("Engineering", result[0].Department);
        Assert.Equal(3, result[0].EmployeeCount);
        Assert.Equal("HR", result[1].Department);
        Assert.Equal("Sales", result[2].Department);
    }

    [Fact]
    public async Task GetHeadcount_ExcludesInactiveByDefault()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Engineering", "US", "USD", 100_000m, isActive: true),
            Emp("Engineering", "US", "USD", 100_000m, isActive: false));
        await db.SaveChangesAsync();

        var active = await sut.GetHeadcountByDepartmentAsync(false, default);
        var all = await sut.GetHeadcountByDepartmentAsync(true, default);

        Assert.Equal(1, active.Single().EmployeeCount);
        Assert.Equal(2, all.Single().EmployeeCount);
    }

    // ---- average salary ------------------------------------------------

    [Fact]
    public async Task GetAverageSalary_ComputesPerDepartment()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Engineering", "US", "USD", 100_000m, 10_000m),
            Emp("Engineering", "US", "USD", 200_000m, 20_000m),
            Emp("Sales", "US", "USD", 80_000m, 5_000m));
        await db.SaveChangesAsync();

        var result = await sut.GetAverageSalaryByDepartmentAsync(false, null, default);

        var eng = result.Single(r => r.Department == "Engineering");
        Assert.Equal(2, eng.EmployeeCount);
        Assert.Equal(150_000m, eng.AverageBaseSalary);
        Assert.Equal(15_000m, eng.AverageBonuses);
        Assert.Equal(165_000m, eng.AverageTotalCompensation);
    }

    [Fact]
    public async Task GetAverageSalary_FiltersByCurrencyWhenProvided()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Engineering", "US", "USD", 100_000m),
            Emp("Engineering", "Germany", "EUR", 80_000m));
        await db.SaveChangesAsync();

        var usd = await sut.GetAverageSalaryByDepartmentAsync(false, "usd", default);

        Assert.Equal(100_000m, usd.Single().AverageBaseSalary);
    }

    // ---- payroll by country --------------------------------------------

    [Fact]
    public async Task GetPayrollByCountry_GroupsByCountryAndCurrency()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Engineering", "US", "USD", 100_000m, 10_000m),
            Emp("Engineering", "US", "USD", 150_000m, 15_000m),
            Emp("Sales", "Germany", "EUR", 80_000m, 5_000m));
        await db.SaveChangesAsync();

        var result = await sut.GetPayrollByCountryAsync(false, default);

        Assert.Equal(2, result.Count);
        var us = result.Single(r => r.Country == "US");
        Assert.Equal("USD", us.Currency);
        Assert.Equal(2, us.EmployeeCount);
        Assert.Equal(250_000m, us.TotalBaseSalary);
        Assert.Equal(25_000m, us.TotalBonuses);
        Assert.Equal(275_000m, us.TotalCompensation);
    }

    // ---- salary distribution -------------------------------------------

    [Fact]
    public async Task GetSalaryDistribution_BandsSalaries()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Eng", "US", "USD", 40_000m),    // < 50k
            Emp("Eng", "US", "USD", 50_000m),    // 50k–100k (min inclusive)
            Emp("Eng", "US", "USD", 99_999m),    // 50k–100k
            Emp("Eng", "US", "USD", 100_000m),   // 100k–150k
            Emp("Eng", "US", "USD", 300_000m));  // 200k+
        await db.SaveChangesAsync();

        var result = await sut.GetSalaryDistributionAsync(false, null, default);

        Assert.Equal(5, result.Count);
        Assert.Equal(1, result.Single(b => b.Band == "< 50k").EmployeeCount);
        Assert.Equal(2, result.Single(b => b.Band == "50k–100k").EmployeeCount);
        Assert.Equal(1, result.Single(b => b.Band == "100k–150k").EmployeeCount);
        Assert.Equal(0, result.Single(b => b.Band == "150k–200k").EmployeeCount);
        Assert.Equal(1, result.Single(b => b.Band == "200k+").EmployeeCount);
    }

    [Fact]
    public async Task GetSalaryDistribution_FiltersByCurrency()
    {
        var (sut, db) = CreateSut();
        db.Employees.AddRange(
            Emp("Eng", "US", "USD", 40_000m),
            Emp("Eng", "India", "INR", 40_000m));
        await db.SaveChangesAsync();

        var usd = await sut.GetSalaryDistributionAsync(false, "USD", default);

        Assert.Equal(1, usd.Single(b => b.Band == "< 50k").EmployeeCount);
    }
}