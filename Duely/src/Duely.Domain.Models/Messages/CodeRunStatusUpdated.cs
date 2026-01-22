using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class CodeRunStatusUpdatedMessage : Message
{
    [JsonPropertyName("run_id")]
    public required int RunId { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required UserCodeRunStatus Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
