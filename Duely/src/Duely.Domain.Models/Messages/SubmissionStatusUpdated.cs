using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class SubmissionStatusUpdatedMessage : Message
{
    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }

    [JsonPropertyName("submission_id")]
    public required int SubmissionId { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required SubmissionStatus Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }
}
