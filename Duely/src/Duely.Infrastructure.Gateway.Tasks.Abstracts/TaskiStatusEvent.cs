using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public sealed class TaskiStatusEvent
{
    [JsonPropertyName("solution_id"), Required]
    public required string SolutionId { get; init; }

    [JsonPropertyName("type"), Required]
    public required string Type { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
