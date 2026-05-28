using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelConfigurationDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("is_deleted")]
    public required bool IsDeleted { get; init; }

    [JsonPropertyName("should_show_opponent_solution")]
    public required bool ShouldShowOpponentSolution { get; init; }

    [JsonPropertyName("max_duration_minutes")]
    public required int MaxDurationMinutes { get; init; }

    [JsonPropertyName("tasks_count")]
    public required int TasksCount { get; init; }

    [JsonPropertyName("tasks_order"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelTasksOrder TasksOrder { get; init; }
}
