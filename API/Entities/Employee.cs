using System;

namespace API.Entities;

public class Employee
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string JobTitle { get; set; }   
    public required string Department { get; set; }
    public required string Country { get; set; }
    public required string Currency { get; set; }

    public decimal BaseSalary { get; set; }
    public decimal Bonuses { get; set; }
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    public decimal TotalCompensation => BaseSalary + Bonuses;
}
