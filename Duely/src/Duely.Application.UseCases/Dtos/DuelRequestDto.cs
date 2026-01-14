using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelRequestDto
{
    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
