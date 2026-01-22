using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class GetSubmissionQuery : IRequest<Result<SubmissionDto>>
{
    public required int SubmissionId { get; init; }
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class GetSubmissionHandler(Context context)
    : IRequestHandler<GetSubmissionQuery, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(GetSubmissionQuery query, CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .Where(s => s.Id == query.SubmissionId && s.Duel.Id == query.DuelId)
            .Include(s => s.User)
            .Include(s => s.Duel)
            .SingleOrDefaultAsync(cancellationToken);
        if (submission is null)
        {
            return new EntityNotFoundError(nameof(Submission), nameof(Submission.Id), query.SubmissionId);
        }

        var isOwner = submission.User.Id == query.UserId;
        return new SubmissionDto
        {
            SubmissionId = submission.Id,
            Solution = isOwner ? submission.Solution : string.Empty,
            Language = submission.Language,
            Status = submission.Status,
            CreatedAt = submission.SubmitTime,
            Message = isOwner ? submission.Message : null,
            Verdict = submission.Verdict,
            IsUpsolving = submission.IsUpsolving
        };
    }
}
