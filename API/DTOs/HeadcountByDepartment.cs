namespace API.DTOs;

public sealed record HeadcountByDepartment(
    string Department,
    int EmployeeCount
);