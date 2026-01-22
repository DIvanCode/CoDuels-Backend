using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Outbox.Payloads;

public sealed class RunCodePayload : OutboxPayload
{
    [JsonPropertyName("run_id")]
    public required int RunId { get; init; }
    
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }
}
