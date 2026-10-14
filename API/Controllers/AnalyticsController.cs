using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Hr")]
public class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    [HttpGet("headcount")]
    public async Task<ActionResult<IReadOnlyList<HeadcountByDepartment>>> GetHeadcount(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await analytics.GetHeadcountByDepartmentAsync(includeInactive, ct));

    [HttpGet("average-salary")]
    public async Task<ActionResult<IReadOnlyList<AverageSalaryByDepartment>>> GetAverageSalary(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? currency = null,
        CancellationToken ct = default)
        => Ok(await analytics.GetAverageSalaryByDepartmentAsync(includeInactive, currency, ct));

    [HttpGet("payroll-by-country")]
    public async Task<ActionResult<IReadOnlyList<PayrollByCountry>>> GetPayrollByCountry(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await analytics.GetPayrollByCountryAsync(includeInactive, ct));

    [HttpGet("salary-distribution")]
    public async Task<ActionResult<IReadOnlyList<SalaryDistributionBand>>> GetSalaryDistribution(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? currency = null,
        CancellationToken ct = default)
        => Ok(await analytics.GetSalaryDistributionAsync(includeInactive, currency, ct));
}