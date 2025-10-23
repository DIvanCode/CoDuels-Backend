using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Submissions;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("solution")]
    public required string Submission { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

}