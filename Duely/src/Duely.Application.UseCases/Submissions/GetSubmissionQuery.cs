using MediatR;
using FluentResults;
using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Submissions;

public sealed class GetSubmissionQuery : IRequest<Result<SubmissionDto>>
{
    public required int SubmissionId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class SubmissionDto 
{
    [JsonPropertyName("submission_id")]
    public int SubmissionId { get; init; }

    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}