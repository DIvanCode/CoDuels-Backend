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
    public required char TaskKey { get; init; }
}

public sealed class GetUserSubmissionsHandler(Context context)
    : IRequestHandler<GetUserSubmissionsQuery, Result<List<SubmissionListItemDto>>>
{
    public async Task<Result<List<SubmissionListItemDto>>> Handle(GetUserSubmissionsQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d => d.Id == query.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }

        if (duel.User1.Id != query.UserId && duel.User2.Id != query.UserId)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }

        var items = await context.Submissions
            .Where(s => s.Duel.Id == duel.Id && s.User.Id == query.UserId && s.TaskKey == query.TaskKey)
            .OrderBy(s => s.SubmitTime)
            .Select(s => new SubmissionListItemDto
            {
                SubmissionId = s.Id,
                Status = s.Status,
                Language = s.Language,
                CreatedAt = s.SubmitTime,
                Verdict = s.Verdict,
                IsUpsolve = s.IsUpsolve
            })
            .ToListAsync(cancellationToken);

        return Result.Ok(items);
    }
}
