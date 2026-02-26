using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class GetIncomingDuelInvitationsQuery : IRequest<Result<List<DuelInvitationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetIncomingDuelInvitationsHandler(Context context)
    : IRequestHandler<GetIncomingDuelInvitationsQuery, Result<List<DuelInvitationDto>>>
{
    public async Task<Result<List<DuelInvitationDto>>> Handle(
        GetIncomingDuelInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var friendlyPendingDuels = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .Include(d => d.User1)
            .Include(d => d.Configuration)
            .Where(d => d.User2.Id == query.UserId && !d.IsAccepted)
            .OrderBy(d => d.Id)
            .Select(d => new DuelInvitationDto
            {
                Type = PendingDuelType.Friendly,
                OpponentNickname = d.User1.Nickname,
                ConfigurationId = d.Configuration != null ? d.Configuration.Id : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var groupPendingDuels = await context.PendingDuels.OfType<GroupPendingDuel>()
            .AsNoTracking()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Where(d =>
                (d.User1.Id == query.UserId && !d.IsAcceptedByUser1) ||
                (d.User2.Id == query.UserId && !d.IsAcceptedByUser2))
            .OrderBy(d => d.Id)
            .Select(d => new DuelInvitationDto
            {
                Type = PendingDuelType.Group,
                OpponentNickname = d.User1.Id == query.UserId ? d.User2.Nickname : d.User1.Nickname,
                ConfigurationId = d.Configuration != null ? d.Configuration.Id : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return friendlyPendingDuels.Concat(groupPendingDuels).ToList();
    }
}
