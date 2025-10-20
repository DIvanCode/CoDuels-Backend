using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Submissions;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("user_id")]
    public required int UserId { get; init; }

    [JsonPropertyName("submission")]
    public required string Submission { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

}