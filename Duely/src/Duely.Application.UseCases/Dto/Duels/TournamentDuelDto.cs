using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dto.Duels;

public sealed class TournamentDuelDto : DuelDto
{
    [JsonPropertyName("is_confirmed")]
    public required IReadOnlyDictionary<Guid, bool> IsConfirmed { get; init; }
}
