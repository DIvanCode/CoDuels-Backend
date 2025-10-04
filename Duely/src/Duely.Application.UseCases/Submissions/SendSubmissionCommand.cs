using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.Submissions;

public sealed record SendSubmissionCommand : IRequest<Result<int>>
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
}
