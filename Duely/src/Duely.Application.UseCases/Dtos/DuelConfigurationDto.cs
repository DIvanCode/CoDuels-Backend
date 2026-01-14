using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelConfigurationDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("should_show_opponent_code")]
    public required bool ShouldShowOpponentCode { get; init; }

    [JsonPropertyName("max_duration_minutes")]
    public required int MaxDurationMinutes { get; init; }

    [JsonPropertyName("task_count")]
    public required int TasksCount { get; init; }

    [JsonPropertyName("task_order"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelTasksOrder TasksOrder { get; init; }

    [JsonPropertyName("tasks")]
    public required Dictionary<char, DuelTaskConfigurationDto> Tasks { get; init; }
}

public sealed class DuelTaskConfigurationDto
{
    [JsonPropertyName("level")]
    public required int Level { get; init; }

    [JsonPropertyName("topics")]
    public required string[] Topics { get; init; }
}
