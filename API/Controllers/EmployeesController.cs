using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Hr")]
public class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<EmployeeListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EmployeeListResponse>> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var response = await employees.ListAsync(page, pageSize, includeInactive, ct);
        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType<EmployeeListItem>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeListItem>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct)
    {
        var shapeError = ValidateShape(request);
        if (shapeError is not null) return shapeError;

        var result = await employees.CreateAsync(request, ct);

        return result switch
        {
            EmployeeCreateResult.Created c =>
                CreatedAtAction(nameof(GetEmployees), new { id = c.Item.Id }, c.Item),

            EmployeeCreateResult.DuplicateEmail d => Conflict(new ProblemDetails
            {
                Title = "Email already in use",
                Detail = $"An employee with email '{d.Email}' already exists.",
                Status = StatusCodes.Status409Conflict,
            }),

            EmployeeCreateResult.InvalidState s => BadRequest(new ProblemDetails
            {
                Title = "Invalid employee state",
                Detail = s.Message,
                Status = StatusCodes.Status400BadRequest,
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id}/terminate")]
    [ProducesResponseType<EmployeeListItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeListItem>> TerminateEmployee(
        string id,
        CancellationToken ct)
    {
        var result = await employees.TerminateAsync(id, ct);

        return result switch
        {
            EmployeeTerminateResult.Terminated t => Ok(t.Item),
            EmployeeTerminateResult.NotFound => NotFound(),

            EmployeeTerminateResult.AlreadyTerminated a => Conflict(new ProblemDetails
            {
                Title = "Employee already terminated",
                Detail = $"Employee was terminated on {a.On:yyyy-MM-dd}.",
                Status = StatusCodes.Status409Conflict,
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
    
    private ActionResult? ValidateShape(CreateEmployeeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) ||
            string.IsNullOrWhiteSpace(r.LastName) ||
            string.IsNullOrWhiteSpace(r.Email) ||
            string.IsNullOrWhiteSpace(r.JobTitle) ||
            string.IsNullOrWhiteSpace(r.Department) ||
            string.IsNullOrWhiteSpace(r.Country) ||
            string.IsNullOrWhiteSpace(r.Currency))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing required fields",
                Detail = "FirstName, LastName, Email, JobTitle, Department, Country, and Currency are required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (!r.Email.Contains('@') || r.Email.Length < 3)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid email",
                Detail = "Email must be a valid address.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (r.BaseSalary < 0 || r.Bonuses < 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid salary",
                Detail = "BaseSalary and Bonuses must be non-negative.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        return null;
    }
}