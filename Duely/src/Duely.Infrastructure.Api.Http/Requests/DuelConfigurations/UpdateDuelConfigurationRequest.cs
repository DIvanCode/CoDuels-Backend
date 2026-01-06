using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Api.Http.Requests.DuelConfigurations;

public sealed class UpdateDuelConfigurationRequest
{
    [JsonPropertyName("show_opponent_code"), Required]
    public required bool ShowOpponentCode { get; init; }
    
    [JsonPropertyName("max_duration_minutes"), Required]
    public required int MaxDurationMinutes { get; init; }
    
    [JsonPropertyName("tasks_count"), Required]
    public required int TasksCount { get; init; }
    
    [JsonPropertyName("tasks_order"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required DuelTasksOrder TasksOrder { get; init; }
    
    [JsonPropertyName("tasks_configurations"), Required]
    public required List<DuelTaskConfiguration> TasksConfigurations { get; init; }
}

