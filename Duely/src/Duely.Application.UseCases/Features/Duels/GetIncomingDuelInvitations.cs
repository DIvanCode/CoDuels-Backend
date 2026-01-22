using Duely.Application.UseCases.Dtos;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetIncomingDuelInvitationsQuery : IRequest<Result<List<DuelInvitationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetIncomingDuelInvitationsHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<GetIncomingDuelInvitationsQuery, Result<List<DuelInvitationDto>>>
{
    public async Task<Result<List<DuelInvitationDto>>> Handle(
        GetIncomingDuelInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var invitations = duelManager
            .GetWaitingUsers()
            .Where(user => user.ExpectedOpponentId == query.UserId && !user.IsOpponentAssigned)
            .OrderBy(user => user.EnqueuedAt)
            .ToList();
        if (invitations.Count == 0)
        {
            return new List<DuelInvitationDto>();
        }

        var invitationsUserIds = invitations.Select(user => user.UserId).ToList();
        var nicknames = await context.Users
            .Where(user => invitationsUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Nickname })
            .ToDictionaryAsync(user => user.Id, user => user.Nickname, cancellationToken);

        return invitations
            .Where(user => nicknames.ContainsKey(user.UserId))
            .Select(user => new DuelInvitationDto
            {
                OpponentNickname = nicknames[user.UserId],
                ConfigurationId = user.ConfigurationId,
                CreatedAt = user.EnqueuedAt
            })
            .ToList();
    }
}
