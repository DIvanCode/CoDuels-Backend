using System.Text.Json.Serialization;
namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("task_id")]
    public int TaskId { get; init; }

    [JsonPropertyName("submission_id")]
    public int SubmissionId { get; init; }

    [JsonPropertyName("solution")]
    public string Solution { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;
}
