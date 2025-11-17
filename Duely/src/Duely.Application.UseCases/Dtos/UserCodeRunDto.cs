using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class UserCodeRunDto
{
    [JsonPropertyName("run_id")]
    public int RunId { get; init; }

    [JsonPropertyName("solution")]
    public string Solution { get; init; } = "";

    [JsonPropertyName("language")]
    public string Language { get; init; } = "";

    [JsonPropertyName("input")]
    public string Input { get; init; } = "";

    [JsonPropertyName("status")]
    public SubmissionStatus Status { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}
