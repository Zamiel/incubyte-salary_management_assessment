using System.IdentityModel.Tokens.Jwt;
using API.Configuration;
using API.Entities;
using API.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace API.Tests.Services;

public class TokenServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

    private static TokenService CreateSut(out FakeTimeProvider clock, int expiryMinutes = 480)
    {
        var settings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = new string('k', 64),
            AccessTokenMinutes = expiryMinutes,
        };

        clock = new FakeTimeProvider(FixedNow);

        var options = Options.Create(settings);
        return new TokenService(options, clock);
    }

    private static User CreateUser() => new()
    {
        Id = "user-123",
        Email = "hr@acme.org",
        DisplayName = "HR Manager",
        Role = UserRole.Hr,
    };

    [Fact]
    public void CreateAccessToken_EmitsExpectedClaims()
    {
        var sut = CreateSut(out _);
        var user = CreateUser();

        var (token, _) = sut.CreateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("user-123", jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("hr@acme.org", jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Hr", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Contains(jwt.Claims, c => c.Type == "jti");
    }

    [Fact]
    public void CreateAccessToken_ExpiresAtConfiguredTime()
    {
        var sut = CreateSut(out _, expiryMinutes: 60);
        var user = CreateUser();

        var (token, expiresAt) = sut.CreateAccessToken(user);

        Assert.Equal(FixedNow.AddMinutes(60), expiresAt);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(FixedNow.AddMinutes(60).UtcDateTime, jwt.ValidTo);
    }

    [Fact]
    public void CreateAccessToken_UsesConfiguredIssuerAndAudience()
    {
        var sut = CreateSut(out _);
        var user = CreateUser();

        var (token, _) = sut.CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }
}