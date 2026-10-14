using API.DTOs;

namespace API.Services;

public interface IAnalyticsService
{
    Task<IReadOnlyList<HeadcountByDepartment>> GetHeadcountByDepartmentAsync(
        bool includeInactive,
        CancellationToken ct);

    Task<IReadOnlyList<AverageSalaryByDepartment>> GetAverageSalaryByDepartmentAsync(
        bool includeInactive,
        string? currency,
        CancellationToken ct);

    Task<IReadOnlyList<PayrollByCountry>> GetPayrollByCountryAsync(
        bool includeInactive,
        CancellationToken ct);

    Task<IReadOnlyList<SalaryDistributionBand>> GetSalaryDistributionAsync(
        bool includeInactive,
        string? currency,
        CancellationToken ct);
}