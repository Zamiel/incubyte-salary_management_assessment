using API.Controllers;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace API.Tests.Controllers;

public class EmployeesControllerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    // ---- helpers --------------------------------------------------------

    private static AppDbContext CreateDb(int employeeCount, int inactiveCount = 0)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);

        for (var i = 0; i < employeeCount; i++)
        {
            db.Employees.Add(new Employee
            {
                FirstName = $"First{i:D4}",
                LastName = $"Last{i:D4}",
                Email = $"e{i}@acme.org",
                JobTitle = "Engineer",
                Department = "Engineering",
                Country = "US",
                Currency = "USD",
                BaseSalary = 100_000m,
                Bonuses = 10_000m,
                HireDate = new DateOnly(2023, 1, 15),
                IsActive = true,
            });
        }

        for (var i = 0; i < inactiveCount; i++)
        {
            db.Employees.Add(new Employee
            {
                FirstName = $"Former{i:D4}",
                LastName = $"FormerLast{i:D4}",
                Email = $"former{i}@acme.org",
                JobTitle = "Engineer",
                Department = "Engineering",
                Country = "US",
                Currency = "USD",
                BaseSalary = 90_000m,
                Bonuses = 0m,
                HireDate = new DateOnly(2021, 6, 1),
                IsActive = false,
                TerminationDate = new DateOnly(2024, 1, 1),
            });
        }

        db.SaveChanges();
        return db;
    }

    private static EmployeesController CreateSut(AppDbContext db)
    {
        var service = new EmployeeService(db, new FakeTimeProvider(FixedNow));
        return new EmployeesController(service);
    }

    private static CreateEmployeeRequest ValidRequest(
        string email = "alice@acme.org",
        bool isActive = true,
        DateOnly? terminationDate = null) => new(
            FirstName: "Alice",
            LastName: "Smith",
            Email: email,
            JobTitle: "Engineer",
            Department: "Engineering",
            Country: "US",
            Currency: "USD",
            BaseSalary: 100_000m,
            Bonuses: 10_000m,
            HireDate: new DateOnly(2023, 1, 15),
            IsActive: isActive,
            TerminationDate: terminationDate);

    // ---- list ----------------------------------------------------------

    [Fact]
    public async Task GetEmployees_ReturnsPaginatedResults()
    {
        await using var db = CreateDb(employeeCount: 120);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(page: 2, pageSize: 50, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(2, response.Page);
        Assert.Equal(50, response.PageSize);
        Assert.Equal(120, response.TotalCount);
        Assert.Equal(50, response.Items.Count);
    }

    [Fact]
    public async Task GetEmployees_ReturnsFirstPageByDefault()
    {
        await using var db = CreateDb(employeeCount: 10);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(default, default, default, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(1, response.Page);
        Assert.Equal(50, response.PageSize);
        Assert.Equal(10, response.Items.Count);
    }

    [Fact]
    public async Task GetEmployees_ClampsOversizedPageSize()
    {
        await using var db = CreateDb(employeeCount: 10);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 99_999, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(200, response.PageSize);
    }

    [Fact]
    public async Task GetEmployees_ExcludesInactiveByDefault()
    {
        await using var db = CreateDb(employeeCount: 5, inactiveCount: 3);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 50, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(5, response.TotalCount);
        Assert.All(response.Items, i => Assert.True(i.IsActive));
    }

    [Fact]
    public async Task GetEmployees_IncludesInactiveWhenRequested()
    {
        await using var db = CreateDb(employeeCount: 5, inactiveCount: 3);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 50, includeInactive: true, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(8, response.TotalCount);
        Assert.Contains(response.Items, i => !i.IsActive);
    }

    [Fact]
    public async Task GetEmployees_ComputesTotalCompensation()
    {
        await using var db = CreateDb(employeeCount: 1);
        var sut = CreateSut(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 10, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        var item = Assert.Single(response.Items);
        Assert.Equal(110_000m, item.TotalCompensation);
        Assert.Equal(new DateOnly(2023, 1, 15), item.HireDate);
    }

    // ---- create --------------------------------------------------------

    [Fact]
    public async Task CreateEmployee_PersistsAndReturnsCreated()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        var result = await sut.CreateEmployee(ValidRequest(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var item = Assert.IsType<EmployeeListItem>(created.Value);

        Assert.Equal("alice@acme.org", item.Email);
        Assert.True(item.IsActive);
        Assert.Null(item.TerminationDate);
        Assert.Equal(1, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task CreateEmployee_NormalizesEmailAndCurrency()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        var request = ValidRequest(email: "  ALICE@ACME.ORG  ") with { Currency = "usd" };

        var result = await sut.CreateEmployee(request, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var item = Assert.IsType<EmployeeListItem>(created.Value);
        Assert.Equal("alice@acme.org", item.Email);
        Assert.Equal("USD", item.Currency);
    }

    [Fact]
    public async Task CreateEmployee_WithDuplicateEmail_ReturnsConflict()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        await sut.CreateEmployee(ValidRequest(), default);
        var second = await sut.CreateEmployee(ValidRequest(), default);

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task CreateEmployee_InactiveWithoutTerminationDate_ReturnsBadRequest()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        var request = ValidRequest(isActive: false, terminationDate: null);

        var result = await sut.CreateEmployee(request, default);

        // Business rule enforced by EmployeeService, mapped to 400 by the controller.
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateEmployee_TerminationBeforeHire_ReturnsBadRequest()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        var request = ValidRequest(
            isActive: false,
            terminationDate: new DateOnly(2020, 1, 1));

        var result = await sut.CreateEmployee(request, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---- terminate -----------------------------------------------------

    [Fact]
    public async Task TerminateEmployee_SetsInactiveAndTerminationDate()
    {
        await using var db = CreateDb(employeeCount: 1);
        var sut = CreateSut(db);
        var employee = await db.Employees.FirstAsync();

        var result = await sut.TerminateEmployee(employee.Id, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<EmployeeListItem>(ok.Value);
        Assert.False(item.IsActive);
        Assert.Equal(new DateOnly(2026, 6, 15), item.TerminationDate);
    }

    [Fact]
    public async Task TerminateEmployee_AlreadyTerminated_ReturnsConflict()
    {
        await using var db = CreateDb(employeeCount: 1);
        var sut = CreateSut(db);
        var employee = await db.Employees.FirstAsync();

        await sut.TerminateEmployee(employee.Id, default);
        var second = await sut.TerminateEmployee(employee.Id, default);

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task TerminateEmployee_UnknownId_ReturnsNotFound()
    {
        await using var db = CreateDb(employeeCount: 0);
        var sut = CreateSut(db);

        var result = await sut.TerminateEmployee("does-not-exist", default);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}