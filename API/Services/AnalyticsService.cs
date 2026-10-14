using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public sealed class AnalyticsService(AppDbContext db) : IAnalyticsService
{
    // Band boundaries. Min is inclusive, Max is exclusive.
    private static readonly (string Label, decimal Min, decimal? Max)[] SalaryBands =
    [
        ("< 50k",      0m,       50_000m),
        ("50k–100k",   50_000m,  100_000m),
        ("100k–150k",  100_000m, 150_000m),
        ("150k–200k",  150_000m, 200_000m),
        ("200k+",      200_000m, null),
    ];

    public async Task<IReadOnlyList<HeadcountByDepartment>> GetHeadcountByDepartmentAsync(
        bool includeInactive,
        CancellationToken ct)
    {
        return await BaseQuery(includeInactive)
            .GroupBy(e => e.Department)
            .Select(g => new HeadcountByDepartment(g.Key, g.Count()))
            .OrderByDescending(x => x.EmployeeCount)
            .ThenBy(x => x.Department)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AverageSalaryByDepartment>> GetAverageSalaryByDepartmentAsync(
        bool includeInactive,
        string? currency,
        CancellationToken ct)
    {
        var query = ApplyCurrency(BaseQuery(includeInactive), currency);

        // SQLite stores decimal as TEXT and doesn't support AVG on it directly.
        // Cast to double in SQL, convert back in memory. Precision loss is
        // irrelevant for reporting purposes.
        var groups = await query
            .GroupBy(e => e.Department)
            .Select(g => new
            {
                Department = g.Key,
                Count = g.Count(),
                TotalBase = g.Sum(e => (double)e.BaseSalary),
                TotalBonuses = g.Sum(e => (double)e.Bonuses),
            })
            .ToListAsync(ct);

        return groups
            .Select(g =>
            {
                var avgBase = g.Count == 0 ? 0m : (decimal)(g.TotalBase / g.Count);
                var avgBonuses = g.Count == 0 ? 0m : (decimal)(g.TotalBonuses / g.Count);
                return new AverageSalaryByDepartment(
                    g.Department,
                    g.Count,
                    avgBase,
                    avgBonuses,
                    avgBase + avgBonuses);
            })
            .OrderByDescending(x => x.AverageBaseSalary)
            .ThenBy(x => x.Department)
            .ToList();
    }

    public async Task<IReadOnlyList<PayrollByCountry>> GetPayrollByCountryAsync(
        bool includeInactive,
        CancellationToken ct)
    {
        var groups = await BaseQuery(includeInactive)
            .GroupBy(e => new { e.Country, e.Currency })
            .Select(g => new
            {
                g.Key.Country,
                g.Key.Currency,
                Count = g.Count(),
                TotalBase = g.Sum(e => (double)e.BaseSalary),
                TotalBonuses = g.Sum(e => (double)e.Bonuses),
            })
            .ToListAsync(ct);

        return groups
            .Select(g =>
            {
                var totalBase = (decimal)g.TotalBase;
                var totalBonuses = (decimal)g.TotalBonuses;
                return new PayrollByCountry(
                    g.Country,
                    g.Currency,
                    g.Count,
                    totalBase,
                    totalBonuses,
                    totalBase + totalBonuses);
            })
            .OrderByDescending(x => x.TotalCompensation)
            .ThenBy(x => x.Country)
            .ToList();
    }

    public async Task<IReadOnlyList<SalaryDistributionBand>> GetSalaryDistributionAsync(
        bool includeInactive,
        string? currency,
        CancellationToken ct)
    {
        var query = ApplyCurrency(BaseQuery(includeInactive), currency);

        // Fetch salaries and band in memory. For 10k rows this is trivial;
        // for millions we'd push the banding into SQL with a CASE expression
        // or maintain a pre-aggregated table.
        var salaries = await query
            .Select(e => e.BaseSalary)
            .ToListAsync(ct);

        return SalaryBands
            .Select(b => new SalaryDistributionBand(
                b.Label,
                b.Min,
                b.Max,
                salaries.Count(s => s >= b.Min && (b.Max is null || s < b.Max))))
            .ToList();
    }

    // ---- helpers --------------------------------------------------------

    private IQueryable<Employee> BaseQuery(bool includeInactive)
    {
        var query = db.Employees.AsNoTracking();
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        return query;
    }

    private static IQueryable<Employee> ApplyCurrency(IQueryable<Employee> query, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return query;
        var normalized = currency.Trim().ToUpperInvariant();
        return query.Where(e => e.Currency == normalized);
    }
}