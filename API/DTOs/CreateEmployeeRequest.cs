namespace API.DTOs;

public sealed record CreateEmployeeRequest
(
    string? FirstName,
    string? LastName,
    string? Email,
    string? JobTitle,
    string? Department,
    string? Country,
    string? Currency,
    decimal BaseSalary,
    decimal Bonuses,
    DateOnly HireDate,
    bool IsActive = true,
    DateOnly? TerminationDate = null
);