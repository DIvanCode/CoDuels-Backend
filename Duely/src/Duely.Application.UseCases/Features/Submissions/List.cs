using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Groups;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class GetUserSubmissionsQuery : IRequest<Result<List<SubmissionListItemDto>>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
    public required char TaskKey { get; init; }
}

public sealed class GetUserSubmissionsHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetUserSubmissionsQuery, Result<List<SubmissionListItemDto>>>
{
    public async Task<Result<List<SubmissionListItemDto>>> Handle(
        GetUserSubmissionsQuery query,
        CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d => d.Id == query.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }

        var isParticipant = duel.User1.Id == query.UserId || duel.User2.Id == query.UserId;
        if (!isParticipant)
        {
            var canViewDuel = await CanViewGroupDuel(query.UserId, query.DuelId, cancellationToken);
            if (!canViewDuel)
            {
                return new ForbiddenError(nameof(Duel), "get submissions", nameof(Duel.Id), query.DuelId);
            }
        }

        var submissionsQuery = context.Submissions.Where(s => s.Duel.Id == duel.Id && s.TaskKey == query.TaskKey);
        if (isParticipant)
        {
            submissionsQuery = submissionsQuery.Where(s => s.User.Id == query.UserId);
        }

        return await submissionsQuery
            .OrderBy(s => s.SubmitTime)
            .Select(s => new SubmissionListItemDto
            {
                SubmissionId = s.Id,
                Status = s.Status,
                Language = s.Language,
                Author = new UserDto
                {
                    Id = s.User.Id,
                    Nickname = s.User.Nickname,
                    Rating = s.User.Rating,
                    CreatedAt = s.User.CreatedAt
                },
                CreatedAt = s.SubmitTime,
                Verdict = s.Verdict,
                IsUpsolving = s.IsUpsolving
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> CanViewGroupDuel(int userId, int duelId, CancellationToken cancellationToken)
    {
        var groupDuel = await context.GroupDuels
            .AsNoTracking()
            .Include(d => d.Group)
            .Where(d => d.Duel.Id == duelId)
            .SingleOrDefaultAsync(cancellationToken);

        if (groupDuel is null)
        {
            return false;
        }

        var membership = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == groupDuel.Group.Id && m.User.Id == userId)
            .SingleOrDefaultAsync(cancellationToken);

        return membership is not null && groupPermissionsService.CanViewDuel(membership);
    }
}
