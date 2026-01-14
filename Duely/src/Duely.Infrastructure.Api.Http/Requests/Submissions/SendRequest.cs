using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Submissions;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("task_key"), Required]
    public required char TaskKey { get; init; }

    [JsonPropertyName("solution"), Required]
    public required string Submission { get; init; }

    [JsonPropertyName("language"), Required]
    public required string Language { get; init; }
}
