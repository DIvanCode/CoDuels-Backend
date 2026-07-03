using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Application.Handlers.Duels.Models;

public sealed class SubmissionDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }
    
    [JsonPropertyName("problem_position")]
    public required int ProblemPosition { get; init; }
    
    [JsonPropertyName("source")]
    public required string Source { get; init; }
    
    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
