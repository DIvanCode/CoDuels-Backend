using System.Text.Json.Serialization;
namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class TestRequest
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; init; } = string.Empty;

    [JsonPropertyName("solution_id")]
    public string SolutionId { get; init; } = string.Empty;

    [JsonPropertyName("solution")]
    public string Solution { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;
}
