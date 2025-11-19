namespace Duely.Domain.Models;

public sealed class User
{
    public int Id { get; init; }
    public required string Nickname { get; set; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public string? RefreshToken { get; set; }
    public int Rating { get; set; } = 1500;
}
