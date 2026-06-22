namespace Duely.Infrastructure.Api.Http.Users.Services.AuthToken;

internal sealed class JwtTokenOptions
{
    public const string SectionName = "User:JwtToken";

    public required string SecretKey { get; init; }
    public int ExpiresHours { get; init; } = 1;
}
