using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Problems.Taski;

internal sealed class TaskiTaskListResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("tasks")]
    public List<TaskiTaskResponse>? Tasks { get; init; }
}
