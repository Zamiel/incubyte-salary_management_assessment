using System;

namespace API.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string DisplayName { get; set; }
    public required string Email { get; set; }

    public string? PasswordHash { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }  
    public DateTimeOffset? LockoutEnd { get; set; }
}
