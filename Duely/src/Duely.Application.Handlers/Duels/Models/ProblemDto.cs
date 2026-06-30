using System.Text.Json.Serialization;

namespace Duely.Application.Handlers.Duels.Models;

public sealed class ProblemDto
{
    [JsonPropertyName("internal_id")]
    public required int InternalId { get; init; }
    
    [JsonPropertyName("system_name")]
    public required string SystemName { get; init; }
    
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
}
