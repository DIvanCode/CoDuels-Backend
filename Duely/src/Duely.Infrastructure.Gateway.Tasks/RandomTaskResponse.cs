using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Tasks;

public class RandomTaskResponse
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; set; }
}