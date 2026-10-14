using API.Data;
using API.Entities;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace API.Tests.Services;

public class AuthServiceTests
{
    private const string TestEmail = "hr@acme.org";
    private const string TestPassword = "Correct-Horse!";

    // ---- Test helpers ----------------------------------------------------

    private static (AuthService Sut, AppDbContext Db, FakeTimeProvider Clock, User User)
        CreateSut(DateTimeOffset? now = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())   // isolated per test
            .Options;

        var db = new AppDbContext(options);
        var hasher = new PasswordHasher<User>();
        var clock = new FakeTimeProvider(now ?? DateTimeOffset.UtcNow);

        var user = new User { Email = TestEmail, DisplayName = "HR Manager", Role = UserRole.Hr };
        user.PasswordHash = hasher.HashPassword(user, TestPassword);

        db.Users.Add(user);
        db.SaveChanges();

        var tokens = new FakeTokenService();
        var sut = new AuthService(db, hasher, tokens, clock, NullLogger<AuthService>.Instance);

        return (sut, db, clock, user);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
            => ("test-token", DateTimeOffset.UtcNow.AddHours(1));
    }

    // ---- Happy path ------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.LoginAsync(TestEmail, TestPassword);

        var success = Assert.IsType<LoginResult.Success>(result);
        Assert.Equal(TestEmail, success.Email);
        Assert.Equal("test-token", success.Token);
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.LoginAsync("HR@ACME.ORG", TestPassword);

        Assert.IsType<LoginResult.Success>(result);
    }

    // ---- Failure paths ---------------------------------------------------

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsInvalidCredentials()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.LoginAsync("nobody@acme.org", TestPassword);

        Assert.IsType<LoginResult.InvalidCredentials>(result);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IncrementsFailureCount()
    {
        var (sut, _, _, user) = CreateSut();

        var result = await sut.LoginAsync(TestEmail, "wrong");

        Assert.IsType<LoginResult.InvalidCredentials>(result);
        Assert.Equal(1, user.AccessFailedCount);
        Assert.NotNull(user.LastFailedAt);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ResetsFailureCount()
    {
        var (sut, _, _, user) = CreateSut();
        user.AccessFailedCount = 3;
        user.LastFailedAt = DateTimeOffset.UtcNow;

        var result = await sut.LoginAsync(TestEmail, TestPassword);

        Assert.IsType<LoginResult.Success>(result);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LastFailedAt);
    }

    // ---- Lockout ---------------------------------------------------------

    [Fact]
    public async Task Login_FifthConsecutiveFailure_LocksAccount()
    {
        var (sut, _, _, user) = CreateSut();
        user.AccessFailedCount = 4;
        user.LastFailedAt = DateTimeOffset.UtcNow;

        var result = await sut.LoginAsync(TestEmail, "wrong");

        Assert.IsType<LoginResult.InvalidCredentials>(result);
        Assert.NotNull(user.LockoutEnd);
        Assert.Equal(0, user.AccessFailedCount);   // reset after lockout
    }

    [Fact]
    public async Task Login_WhenLockedOut_ReturnsLockedOut()
    {
        var now = DateTimeOffset.UtcNow;
        var (sut, _, _, user) = CreateSut(now);
        user.LockoutEnd = now.AddMinutes(10);

        var result = await sut.LoginAsync(TestEmail, TestPassword);  // even the right password

        var locked = Assert.IsType<LoginResult.LockedOut>(result);
        Assert.Equal(user.LockoutEnd, locked.Until);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_AllowsRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var (sut, _, clock, user) = CreateSut(now);
        user.LockoutEnd = now.AddMinutes(10);

        clock.Advance(TimeSpan.FromMinutes(11));   // move past the lockout

        var result = await sut.LoginAsync(TestEmail, TestPassword);

        Assert.IsType<LoginResult.Success>(result);
    }

    // ---- Stale failure window (the fix) ----------------------------------

    [Fact]
    public async Task Login_FailureOutsideWindow_StartsFreshStreak()
    {
        var now = DateTimeOffset.UtcNow;
        var (sut, _, _, user) = CreateSut(now);

        // Four failures, 20 minutes ago — outside the 15-minute window.
        user.AccessFailedCount = 4;
        user.LastFailedAt = now.AddMinutes(-20);

        var result = await sut.LoginAsync(TestEmail, "wrong");

        Assert.IsType<LoginResult.InvalidCredentials>(result);
        Assert.Null(user.LockoutEnd);              // NOT locked out
        Assert.Equal(1, user.AccessFailedCount);   // fresh streak from 1
    }

    [Fact]
    public async Task Login_FailureWithinWindow_ContinuesStreak()
    {
        var now = DateTimeOffset.UtcNow;
        var (sut, _, _, user) = CreateSut(now);

        user.AccessFailedCount = 4;
        user.LastFailedAt = now.AddMinutes(-5);    // inside the window

        var result = await sut.LoginAsync(TestEmail, "wrong");

        Assert.IsType<LoginResult.InvalidCredentials>(result);
        Assert.NotNull(user.LockoutEnd);           // locked out
    }
}