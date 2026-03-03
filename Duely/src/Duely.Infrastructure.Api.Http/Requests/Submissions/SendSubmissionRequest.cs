using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels;

namespace Duely.Infrastructure.Api.Http.Requests.Submissions;

public sealed class SendSubmissionRequest
{
    [JsonPropertyName("task_key"), Required]
    public required char TaskKey { get; init; }

    [JsonPropertyName("solution"), Required]
    public required string Solution { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required Language Language { get; init; }
}
