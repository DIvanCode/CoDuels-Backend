using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class UserCodeRunListItemDto
{
    [JsonPropertyName("run_id")]
    public int RunId { get; init; }

    [JsonPropertyName("status")]
    public SubmissionStatus Status { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}
