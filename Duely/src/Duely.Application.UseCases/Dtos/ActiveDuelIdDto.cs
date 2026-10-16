using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class ActiveDuelIdDto
{
    [JsonPropertyName("duel_id")]
    public int? DuelId { get; init; }
}
