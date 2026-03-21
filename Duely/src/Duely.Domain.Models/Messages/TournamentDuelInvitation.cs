using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class TournamentDuelInvitationMessage : Message
{
    [JsonPropertyName("tournament_id")]
    public required int TournamentId { get; init; }

    [JsonPropertyName("tournament_name")]
    public required string TournamentName { get; init; }

    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}
