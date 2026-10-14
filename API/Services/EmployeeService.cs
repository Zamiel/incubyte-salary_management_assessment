using System.Linq.Expressions;
using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public sealed class EmployeeService(AppDbContext db, TimeProvider clock) : IEmployeeService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // Shared projection. Used as an expression tree in LINQ (translates to SQL),
    // and compiled for in-memory use after Create/Terminate.
    private static readonly Expression<Func<Employee, EmployeeListItem>> Projection =
        e => new EmployeeListItem(
            e.Id,
            e.FirstName,
            e.LastName,
            e.Email,
            e.JobTitle,
            e.Department,
            e.Country,
            e.Currency,
            e.BaseSalary,
            e.Bonuses,
            e.BaseSalary + e.Bonuses,
            e.HireDate,
            e.TerminationDate,
            e.IsActive);

    private static readonly Func<Employee, EmployeeListItem> ProjectionCompiled =
        Projection.Compile();

    public async Task<EmployeeListResponse> ListAsync(
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = db.Employees.AsNoTracking();
        if (!includeInactive)
            query = query.Where(e => e.IsActive);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return new EmployeeListResponse(page, pageSize, total, items);
    }

    public async Task<EmployeeCreateResult> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken ct)
    {
        var email = request.Email!.Trim().ToLowerInvariant();

        if (await db.Employees.AnyAsync(e => e.Email == email, ct))
            return new EmployeeCreateResult.DuplicateEmail(email);

        if (!request.IsActive && request.TerminationDate is null)
            return new EmployeeCreateResult.InvalidState(
                "An inactive employee must have a TerminationDate.");

        if (request.IsActive && request.TerminationDate is not null)
            return new EmployeeCreateResult.InvalidState(
                "An active employee cannot have a TerminationDate.");

        if (request.TerminationDate is { } term && term < request.HireDate)
            return new EmployeeCreateResult.InvalidState(
                "TerminationDate cannot be earlier than HireDate.");

        var employee = new Employee
        {
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = email,
            JobTitle = request.JobTitle!.Trim(),
            Department = request.Department!.Trim(),
            Country = request.Country!.Trim(),
            Currency = request.Currency!.Trim().ToUpperInvariant(),
            BaseSalary = request.BaseSalary,
            Bonuses = request.Bonuses,
            HireDate = request.HireDate,
            IsActive = request.IsActive,
            TerminationDate = request.IsActive ? null : request.TerminationDate,
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        return new EmployeeCreateResult.Created(ProjectionCompiled(employee));
    }

    public async Task<EmployeeTerminateResult> TerminateAsync(
        string id,
        CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, ct);

        if (employee is null)
            return new EmployeeTerminateResult.NotFound();

        if (!employee.IsActive)
            return new EmployeeTerminateResult.AlreadyTerminated(
                employee.TerminationDate ?? DateOnly.FromDateTime(clock.GetUtcNow().DateTime));

        employee.IsActive = false;
        employee.TerminationDate = DateOnly.FromDateTime(clock.GetUtcNow().DateTime);
        await db.SaveChangesAsync(ct);

        return new EmployeeTerminateResult.Terminated(ProjectionCompiled(employee));
    }
}