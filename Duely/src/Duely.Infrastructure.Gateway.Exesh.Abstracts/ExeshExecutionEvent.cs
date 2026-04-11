using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Exesh.Abstracts;

public sealed class ExeshExecutionEvent
{
    [JsonPropertyName("message_id"), Required]
    public required int EventId { get; init; }

    [JsonPropertyName("message"), Required]
    public required ExeshStatusEvent Event { get; init; }
}
