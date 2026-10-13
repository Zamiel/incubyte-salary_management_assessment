namespace API.DTOs;

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User);

public sealed record AuthenticatedUser(string Id, string Email, string DisplayName);