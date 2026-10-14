using API.Controllers;
using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests.Controllers;

public class EmployeesControllerTests
{
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
            });
        }

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task GetEmployees_ReturnsPaginatedResults()
    {
        await using var db = CreateDb(employeeCount: 120);
        var sut = new EmployeesController(db);

        var result = await sut.GetEmployees(page: 2, pageSize: 50, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(2, response.Page);
        Assert.Equal(50, response.PageSize);
        Assert.Equal(120, response.TotalCount);
        Assert.Equal(50, response.Items.Count);
    }

    [Fact]
    public async Task GetEmployees_ClampsOversizedPageSize()
    {
        await using var db = CreateDb(employeeCount: 10);
        var sut = new EmployeesController(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 99_999, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        Assert.Equal(200, response.PageSize);
    }

    [Fact]
    public async Task GetEmployees_ExcludesInactiveByDefault()
    {
        await using var db = CreateDb(employeeCount: 5, inactiveCount: 3);
        var sut = new EmployeesController(db);

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
        var sut = new EmployeesController(db);

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
        var sut = new EmployeesController(db);

        var result = await sut.GetEmployees(page: 1, pageSize: 10, includeInactive: false, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmployeeListResponse>(ok.Value);

        var item = Assert.Single(response.Items);
        Assert.Equal(110_000m, item.TotalCompensation);
        Assert.Equal(new DateOnly(2023, 1, 15), item.HireDate);
    }
}