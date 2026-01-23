using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("is_rated")]
    public required bool IsRated { get; init; }
    
    [JsonPropertyName("should_show_opponent_solution")]
    public required bool ShouldShowOpponentSolution { get; init; }

    [JsonPropertyName("participants")]
    public required UserDto[] Participants { get; init; }

    [JsonPropertyName("winner_id")]
    public int? WinnerId { get; set; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("deadline_time")]
    public required DateTime DeadlineTime { get; init; }

    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; set; }
    
    [JsonPropertyName("rating_changes")]
    public required Dictionary<int, Dictionary<DuelResult, int>> RatingChanges { get; init; }
    
    [JsonPropertyName("tasks")]
    public required Dictionary<char, DuelTaskDto> Tasks { get; init; }

    [JsonPropertyName("solutions")]
    public required Dictionary<char, DuelTaskSolutionDto> Solutions { get; init; }

    [JsonPropertyName("opponent_solutions")]
    public Dictionary<char, DuelTaskSolutionDto>? OpponentSolutions { get; init; }
}

public sealed class DuelTaskSolutionDto
{
    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
