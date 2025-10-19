using System.Text.Json.Serialization;
namespace Duely.Infrastructure.Gateway.Tasks.Abstracts.Messages;

public sealed class TaskiStatusEvent
{
    [JsonPropertyName("solution_id")]
    public string SolutionId { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
