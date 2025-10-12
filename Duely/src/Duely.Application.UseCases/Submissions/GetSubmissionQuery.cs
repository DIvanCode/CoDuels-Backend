using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Submissions;

public sealed class GetSubmissionQuery : IRequest<Result<SubmissionDto>>
{
    public required int SubmissionId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class SubmissionDto 
{
    public int SubmissionId { get; init; }
    public required string Solution { get; init; }
    public required string Language { get; init; }
    public string? Verdict { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
}