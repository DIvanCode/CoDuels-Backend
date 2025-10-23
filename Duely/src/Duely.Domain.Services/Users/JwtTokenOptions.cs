namespace Duely.Domain.Services.Users;

public sealed class JwtTokenOptions
{
    public const string SectionName = "JwtToken";

    public required string SecretKey { get; init; }
    public int ExpiresHours { get; init; } = 1;
    public string IdClaim { get; init; } = "id";
}
