using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.MessageBus.Kafka;


public sealed class ExeshStatusEvent
{
    [JsonPropertyName("execution_id"), Required]
    public required string ExecutionId { get; init; }

    [JsonPropertyName("type"), Required]
    public required string Type { get; init; }

    [JsonPropertyName("status")]    
    public string? Status { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
