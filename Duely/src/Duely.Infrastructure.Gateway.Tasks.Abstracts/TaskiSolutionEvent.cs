using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public sealed class TaskiSolutionEvent
{
    [JsonPropertyName("message_id"), Required]
    public required int EventId { get; init; }

    [JsonPropertyName("message"), Required]
    public required TaskiStatusEvent Event { get; init; }
}
