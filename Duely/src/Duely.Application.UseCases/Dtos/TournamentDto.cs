using System.Text.Json.Serialization;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Dtos;

public sealed class TournamentDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("status")]
    public required TournamentStatus Status { get; init; }

    [JsonPropertyName("group_id")]
    public required int? GroupId { get; init; }

    [JsonPropertyName("kind")]
    public required TournamentKind Kind { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("starts_at")]
    public DateTime? StartsAt { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("created_by")]
    public required UserDto CreatedBy { get; init; }

    [JsonPropertyName("participants")]
    public required List<UserDto> Participants { get; init; }

    [JsonPropertyName("matchmaking_type")]
    public required TournamentMatchmakingType MatchmakingType { get; init; }

    [JsonPropertyName("duel_configuration_id")]
    public required int? DuelConfigurationId { get; init; }
}

public enum TournamentKind
{
    Group = 1,
    Global = 2
}
