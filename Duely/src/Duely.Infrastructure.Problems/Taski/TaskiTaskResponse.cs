using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Problems.Taski;

internal sealed class TaskiTaskResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("type"), JsonConverter(typeof(TaskiTaskTypeJsonStringEnumConverter))]
    public required TaskiTaskType Type { get; init; }
}

internal enum TaskiTaskType
{
    WriteCode = 0
}

internal sealed class TaskiTaskTypeJsonStringEnumConverter()
    : JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower);
