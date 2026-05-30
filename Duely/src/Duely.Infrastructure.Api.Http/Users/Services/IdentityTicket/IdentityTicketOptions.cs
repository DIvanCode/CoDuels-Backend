namespace Duely.Infrastructure.Api.Http.Users.Services.IdentityTicket;

internal sealed class IdentityTicketOptions
{
    public const string SectionName = "IdentityTicket";

    public int ExpiresMinutes { get; init; } = 5;
}
