using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public sealed record CreateEmployeeRequest
(
    [Required, StringLength(100, MinimumLength = 1)] string? FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string? LastName,
    [Required, EmailAddress, StringLength(256)] string? Email,
    [Required] string? JobTitle,
    [Required] string? Department,
    [Required] string? Country,
    [Required, StringLength(3, MinimumLength = 3)] string? Currency,
    [Range(0, double.MaxValue)] decimal BaseSalary,
    [Range(0, double.MaxValue)] decimal Bonuses,
    DateOnly HireDate,
    bool IsActive = true,
    DateOnly? TerminationDate = null
);