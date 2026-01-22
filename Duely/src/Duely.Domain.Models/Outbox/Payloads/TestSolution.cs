using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Outbox.Payloads;

public sealed class TestSolutionPayload : OutboxPayload
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }
    
    [JsonPropertyName("submission_id")]
    public required int SubmissionId { get; init; }
    
    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
