using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelRequestDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("configuration_id")]
    public required int ConfigurationId { get; init; }

    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}

public sealed class PendingDuelRequestsDto
{
    [JsonPropertyName("incoming")]
    public required List<DuelRequestDto> Incoming { get; init; }

    [JsonPropertyName("outgoing")]
    public required List<DuelRequestDto> Outgoing { get; init; }
}
