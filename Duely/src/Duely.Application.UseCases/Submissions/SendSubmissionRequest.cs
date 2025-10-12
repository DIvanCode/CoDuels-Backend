using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Submissions;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("submission")]
    public required string Submission { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    public int UserId { get; init; }= 0; // временное
}