namespace API.DTOs;

public sealed record EmployeeListItem
(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string JobTitle,
    string Department,
    string Country,
    string Currency,
    decimal BaseSalary,
    decimal Bonuses,
    decimal TotalCompensation,
    DateOnly HireDate,
    bool IsActive
);