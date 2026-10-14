namespace API.DTOs;

public sealed record EmployeeListResponse
(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<EmployeeListItem> Items
);