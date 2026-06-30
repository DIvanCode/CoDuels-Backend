using System.Text.Json.Serialization;

namespace Duely.Application.Handlers.Duels.Models;

public sealed class DuelProblemDto
{
    [JsonPropertyName("problem")]
    public required ProblemDto Problem { get; init; }
    
    [JsonPropertyName("position")]
    public required int Position { get; init; }
}
