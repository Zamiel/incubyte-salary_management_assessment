using System.Security.Claims;
using API.Data;
using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, TimeProvider clock) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Problem(title: "Invalid credentials",
                           detail: "Email and password are required.",
                           statusCode: StatusCodes.Status400BadRequest);

        var result = await authService.LoginAsync(request.Email, request.Password, ct);

        return result switch
        {
            LoginResult.Success s => Ok(new LoginResponse(
                s.Token, s.ExpiresAt, new AuthenticatedUser(s.UserId, s.Email, s.DisplayName))),

            LoginResult.LockedOut l => Problem(
                title: "Account locked",
                detail: "Too many failed sign-in attempts. Try again shortly.",
                statusCode: StatusCodes.Status423Locked,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = Math.Max(0, (l.Until - clock.GetUtcNow()).TotalSeconds),
                }),

            _ => Unauthorized(),
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me([FromServices] AppDbContext db, CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(sub)) return Unauthorized();

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == sub)
            .Select(u => new AuthenticatedUser(u.Id, u.Email, u.DisplayName))
            .SingleOrDefaultAsync(ct);

        return user is null ? Unauthorized() : Ok(user);
    }
}