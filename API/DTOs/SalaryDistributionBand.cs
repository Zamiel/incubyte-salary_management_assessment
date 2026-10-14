namespace API.DTOs;

public sealed record SalaryDistributionBand(
    string Band,
    decimal MinSalary,
    decimal? MaxSalary,
    int EmployeeCount
);