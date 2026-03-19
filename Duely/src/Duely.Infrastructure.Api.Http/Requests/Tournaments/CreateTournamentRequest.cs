using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models.Tournaments;

namespace Duely.Infrastructure.Api.Http.Requests.Tournaments;

public sealed class CreateTournamentRequest
{
    [JsonPropertyName("name"), Required]
    public required string Name { get; init; }

    [JsonPropertyName("group_id"), Required]
    public required int GroupId { get; init; }

    [JsonPropertyName("matchmaking_type"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required TournamentMatchmakingType MatchmakingType { get; init; }

    [JsonPropertyName("participants"), Required]
    public required List<string> Participants { get; init; }

    [JsonPropertyName("duel_configuration_id")]
    public int? DuelConfigurationId { get; init; }
}
