using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelInvitationDto
{
    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
