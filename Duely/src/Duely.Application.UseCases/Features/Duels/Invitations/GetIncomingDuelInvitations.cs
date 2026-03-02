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
        return await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .Include(d => d.User1)
            .Include(d => d.Configuration)
            .Where(d => d.User2.Id == query.UserId && !d.IsAccepted)
            .OrderBy(d => d.Id)
            .Select(d => new DuelInvitationDto
            {
                OpponentNickname = d.User1.Id == query.UserId ? d.User2.Nickname : d.User1.Nickname,
                ConfigurationId = d.Configuration != null ? d.Configuration.Id : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
