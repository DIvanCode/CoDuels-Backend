using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class GetUserSubmissionsQuery : IRequest<Result<List<SubmissionListItemDto>>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class GetUserSubmissionsHandler(Context context)
    : IRequestHandler<GetUserSubmissionsQuery, Result<List<SubmissionListItemDto>>>
{
    public async Task<Result<List<SubmissionListItemDto>>> Handle(GetUserSubmissionsQuery query, CancellationToken cancellationToken)
    {
        var duelExistsForUser = await context.Duels
            .AnyAsync(d => d.Id == query.DuelId && ((d.User1 != null && d.User1.Id == query.UserId) || (d.User2 != null && d.User2.Id == query.UserId)), cancellationToken);
        if (!duelExistsForUser)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }
        var items = await context.Submissions
            .Where(s => s.Duel.Id == query.DuelId && s.User.Id == query.UserId)
            .OrderBy(s => s.SubmitTime)
            .Select(s => new SubmissionListItemDto
            {
                SubmissionId = s.Id,
                Status = s.Status,
                CreatedAt = s.SubmitTime,
                Verdict = s.Status == SubmissionStatus.Done ? s.Verdict : null
            })
            .ToListAsync(cancellationToken);

        return Result.Ok(items);
    }
}
