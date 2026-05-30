namespace Duely.Infrastructure.Api.Http.Users.Services.RefreshToken;

internal sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int ExpiresDays { get; init; } = 7;
}
