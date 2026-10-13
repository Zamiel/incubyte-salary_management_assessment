namespace API.Services;

public abstract record LoginResult
{
    public sealed record Success(string UserId, string Email, string DisplayName,
                                 string Token, DateTimeOffset ExpiresAt) : LoginResult;
    public sealed record InvalidCredentials : LoginResult;
    public sealed record LockedOut(DateTimeOffset Until) : LoginResult;
}

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
}