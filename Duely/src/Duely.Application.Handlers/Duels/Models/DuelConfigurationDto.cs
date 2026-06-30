using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Application.Handlers.Duels.Models;

public sealed class DuelConfigurationDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("is_rated")]
    public required bool IsRated { get; init; }
    
    [JsonPropertyName("show_opponent_solution")]
    public required bool ShowOpponentSolution { get; init; }
    
    [JsonPropertyName("duration_minutes")]
    public required int DurationMinutes { get; init; }
    
    [JsonPropertyName("problems_count")]
    public required int ProblemsCount { get; init; }
    
    [JsonPropertyName("problems_order")]
    public required ProblemsOrder ProblemsOrder { get; init; }
}
