using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public sealed record TaskListResponse
{
    [JsonPropertyName("tasks"), Required]
    public required List<TaskResponse> Tasks { get; init; }
}
