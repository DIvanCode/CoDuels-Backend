using System.Text.Json.Serialization;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;

namespace Duely.Application.UseCases.Dtos;

public class SubmissionListItemDto
{
    [JsonPropertyName("submission_id")]
    public required int SubmissionId { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required SubmissionStatus Status { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }

    [JsonPropertyName("author")]
    public required UserDto Author { get; init; }
    
    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("is_upsolving")]
    public required bool IsUpsolving { get; set; }
}

public sealed class AdminSubmissionListItemDto : SubmissionListItemDto
{
    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }

    [JsonPropertyName("task_key")]
    public required char TaskKey { get; init; }
}
