using System.Text.Json.Serialization;
namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; init; } = string.Empty;

    [JsonPropertyName("submission_id")]
    public string SubmissionId { get; init; } = string.Empty;

    [JsonPropertyName("solution")]
    public string Solution { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;
}
