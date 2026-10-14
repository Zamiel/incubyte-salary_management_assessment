using API.DTOs;

namespace API.Services;

public interface IEmployeeService
{
    Task<EmployeeListResponse> ListAsync(
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken ct);

    Task<EmployeeCreateResult> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken ct);

    Task<EmployeeTerminateResult> TerminateAsync(
        string id,
        CancellationToken ct);
}

public abstract record EmployeeCreateResult
{
    public sealed record Created(EmployeeListItem Item) : EmployeeCreateResult;
    public sealed record DuplicateEmail(string Email) : EmployeeCreateResult;
    public sealed record InvalidState(string Message) : EmployeeCreateResult;
}

public abstract record EmployeeTerminateResult
{
    public sealed record Terminated(EmployeeListItem Item) : EmployeeTerminateResult;
    public sealed record AlreadyTerminated(DateOnly On) : EmployeeTerminateResult;
    public sealed record NotFound : EmployeeTerminateResult;
}