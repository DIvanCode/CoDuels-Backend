using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Api.Http.Requests.DuelConfigurations;

public sealed class CreateDuelConfigurationRequest
{
    [JsonPropertyName("should_show_opponent_solution"), Required]
    public required bool ShouldShowOpponentSolution { get; init; }
    
    [JsonPropertyName("max_duration_minutes"), Required]
    public required int MaxDurationMinutes { get; init; }
    
    [JsonPropertyName("tasks_count"), Required]
    public required int TasksCount { get; init; }
    
    [JsonPropertyName("tasks_order"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required DuelTasksOrder TasksOrder { get; init; }
    
    [JsonPropertyName("tasks_configurations"), Required]
    public required Dictionary<char, DuelTaskConfiguration> TasksConfigurations { get; init; }
}
