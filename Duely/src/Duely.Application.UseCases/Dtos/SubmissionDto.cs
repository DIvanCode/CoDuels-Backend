using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class SubmissionDto
{
    [JsonPropertyName("submission_id")]
    public required int SubmissionId { get; init; }

    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required SubmissionStatus Status { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }
}
