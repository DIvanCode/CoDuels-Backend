using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class RankedDuelSearcherDto
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }

    [JsonPropertyName("rating")]
    public required int Rating { get; init; }

    [JsonPropertyName("search_started_at")]
    public required DateTime SearchStartedAt { get; init; }
}
