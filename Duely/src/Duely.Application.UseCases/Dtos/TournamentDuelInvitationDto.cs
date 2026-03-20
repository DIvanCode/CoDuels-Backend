using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class TournamentDuelInvitationDto
{
    [JsonPropertyName("tournament_id")]
    public required int TournamentId { get; init; }

    [JsonPropertyName("tournament_name")]
    public required string TournamentName { get; init; }

    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
