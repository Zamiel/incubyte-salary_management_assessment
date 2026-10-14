using API.Data;
using API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Hr")]
public class EmployeesController(AppDbContext context) : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int FirstPage = 1;
    private const int MaxPageSize = 200;

    [HttpGet]
    [ProducesResponseType<EmployeeListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EmployeeListResponse>> GetEmployees(
        [FromQuery] int page = FirstPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (page < FirstPage) 
        {
            page = FirstPage;
        }

        if (pageSize < 1) 
        {
            pageSize = DefaultPageSize;
        }
        
        if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        var query = context.Employees.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmployeeListItem(
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
                e.IsActive))
            .ToListAsync(ct);

        return Ok(new EmployeeListResponse(page, pageSize, total, items));
    }
}