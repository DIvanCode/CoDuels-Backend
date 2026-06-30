using System.Text.Json.Serialization;

namespace Duely.Application.Handlers.Duels.Models.DuelParticipants;

public sealed class RankedDuelParticipantDto : DuelParticipantDto
{
    [JsonPropertyName("initial_rating")]
    public required int InitialRating { get; init; }
}
