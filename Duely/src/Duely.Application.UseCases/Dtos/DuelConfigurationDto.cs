using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelConfigurationDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("show_opponent_code")]
    public required bool ShowOpponentCode { get; init; }

    [JsonPropertyName("max_duration_minutes")]
    public required int MaxDurationMinutes { get; init; }

    [JsonPropertyName("task_count")]
    public required int TasksCount { get; init; }

    [JsonPropertyName("task_order"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelTasksOrder TasksOrder { get; init; }

    [JsonPropertyName("tasks")]
    public required List<DuelTaskConfigurationDto> Tasks { get; init; }
}

public sealed class DuelTaskConfigurationDto
{
    [JsonPropertyName("order")]
    public required int Order { get; init; }

    [JsonPropertyName("level")]
    public required int Level { get; init; }

    [JsonPropertyName("topics")]
    public required string[] Topics { get; init; }
}

