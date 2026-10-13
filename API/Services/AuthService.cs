using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    // Verified against when the email doesn't exist, so timing is similar.
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User { DisplayName = "_", Email = "_" },
                                                "timing-equalisation-only");

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var now = clock.GetUtcNow();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || user.PasswordHash is null)
        {
            // Burn roughly the same amount of CPU as a real verify would.
            passwordHasher.VerifyHashedPassword(new User { DisplayName = "_", Email = "_" },
                                                DummyHash, password);
            return new LoginResult.InvalidCredentials();
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            return new LoginResult.LockedOut(lockoutEnd);

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (verification == PasswordVerificationResult.Failed)
        {
            if (user.LastFailedAt is { } lastFailed && (now - lastFailed) > FailureWindow)
            {
                user.AccessFailedCount = 0;
            }    

            user.AccessFailedCount++;
            user.LastFailedAt = now;

            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
                user.AccessFailedCount = 0;
                user.LastFailedAt = null;
                logger.LogWarning("Locked out {UserId} for {Minutes} min",
                    user.Id, LockoutDuration.TotalMinutes);
            }

            await db.SaveChangesAsync(ct);
            return new LoginResult.InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);   
        }

        user.AccessFailedCount = 0;
        user.LastFailedAt = null;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokenService.CreateAccessToken(user);
        logger.LogInformation("Login OK for {UserId}", user.Id);

        return new LoginResult.Success(user.Id, user.Email, user.DisplayName, token, expiresAt);
    }
}