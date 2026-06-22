namespace Duely.Infrastructure.Api.Http.Users.Services.RefreshToken;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "User:RefreshToken";

    public int ExpiresDays { get; init; } = 7;
}
