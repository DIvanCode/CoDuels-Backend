namespace Duely.Infrastructure.Api.Http.Requests.Duels;

public sealed class CreateDuelRequest
{
    public required string OpponentNickname { get; init; }
    public required int ConfigurationId { get; init; }
}
