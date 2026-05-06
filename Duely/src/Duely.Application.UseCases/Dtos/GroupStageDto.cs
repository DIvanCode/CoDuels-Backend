using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupStageDto
{
    [JsonPropertyName("standings")]
    public required List<GroupStageStandingDto> Standings { get; init; }

    [JsonPropertyName("current_duels")]
    public required List<GroupStageDuelDto> CurrentDuels { get; init; }

    [JsonPropertyName("past_duels")]
    public required List<GroupStageDuelDto> PastDuels { get; init; }
}

public sealed class GroupStageStandingDto
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }

    [JsonPropertyName("wins")]
    public required int Wins { get; init; }

    [JsonPropertyName("draws")]
    public required int Draws { get; init; }

    [JsonPropertyName("losses")]
    public required int Losses { get; init; }

    [JsonPropertyName("points")]
    public required int Points { get; init; }
}

public sealed class GroupStageDuelDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("user1")]
    public required UserDto User1 { get; init; }

    [JsonPropertyName("user2")]
    public required UserDto User2 { get; init; }

    [JsonPropertyName("winner_id")]
    public int? WinnerId { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("deadline_time")]
    public required DateTime DeadlineTime { get; init; }

    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; init; }
}
