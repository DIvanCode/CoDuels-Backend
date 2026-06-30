using System.Text.Json.Serialization;
using Duely.Application.Handlers.Duels.Models.DuelParticipants;
using Duely.Application.Handlers.Users.Models;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Application.Handlers.Duels.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RankedDuelParticipantDto), nameof(DuelType.Ranked))]
public abstract class DuelParticipantDto
{
    [JsonPropertyName("user")]
    public required UserShortDto User { get; init; }
    
    [JsonPropertyName("is_ready")]
    public required bool IsReady { get; init; }
}
