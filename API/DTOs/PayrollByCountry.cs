namespace API.DTOs;

public sealed record PayrollByCountry(
    string Country,
    string Currency,
    int EmployeeCount,
    decimal TotalBaseSalary,
    decimal TotalBonuses,
    decimal TotalCompensation
);