using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels.Pending;

namespace Duely.Application.UseCases.Dtos;

public sealed class PendingDuelDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("type"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required PendingDuelType Type { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("user1")]
    public required UserDto User1 { get; init; }

    [JsonPropertyName("user2")]
    public required UserDto User2 { get; init; }

    [JsonPropertyName("is_accepted_by_user1")]
    public required bool IsAcceptedByUser1 { get; init; }

    [JsonPropertyName("is_accepted_by_user2")]
    public required bool IsAcceptedByUser2 { get; init; }

    [JsonPropertyName("group_id")]
    public int? GroupId { get; init; }

    [JsonPropertyName("group_name")]
    public string? GroupName { get; init; }

    [JsonPropertyName("tournament_id")]
    public int? TournamentId { get; init; }

    [JsonPropertyName("tournament_name")]
    public string? TournamentName { get; init; }
}
