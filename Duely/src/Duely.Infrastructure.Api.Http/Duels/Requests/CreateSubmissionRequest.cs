using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Infrastructure.Api.Http.Duels.Requests;

public sealed class CreateSubmissionRequest
{
    [JsonPropertyName("problem_position")]
    public required int ProblemPosition { get; init; }
    
    [JsonPropertyName("source")]
    public required string Source { get; init; }
    
    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
