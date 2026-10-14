namespace API.DTOs;

public sealed record AverageSalaryByDepartment(
    string Department,
    int EmployeeCount,
    decimal AverageBaseSalary,
    decimal AverageBonuses,
    decimal AverageTotalCompensation
);