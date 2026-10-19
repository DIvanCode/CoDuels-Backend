using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class ActiveDuelIdsDto
{
    [JsonPropertyName("duel_ids")]
    public required List<int> DuelIds { get; init; }
}
