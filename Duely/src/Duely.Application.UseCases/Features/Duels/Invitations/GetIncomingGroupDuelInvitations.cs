using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class GetIncomingGroupDuelInvitationsQuery : IRequest<Result<List<GroupDuelInvitationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetIncomingGroupDuelInvitationsHandler(Context context)
    : IRequestHandler<GetIncomingGroupDuelInvitationsQuery, Result<List<GroupDuelInvitationDto>>>
{
    public async Task<Result<List<GroupDuelInvitationDto>>> Handle(
        GetIncomingGroupDuelInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        return await context.PendingDuels.OfType<GroupPendingDuel>()
            .AsNoTracking()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Group)
            .Where(d =>
                (d.User1.Id == query.UserId && !d.IsAcceptedByUser1) ||
                (d.User2.Id == query.UserId && !d.IsAcceptedByUser2))
            .OrderBy(d => d.Id)
            .Select(d => new GroupDuelInvitationDto
            {
                Group = new GroupDto
                {
                    Id = d.Group.Id,
                    Name = d.Group.Name,
                    UserRole = context.GroupMemberships
                        .Where(m => m.Group.Id == d.Group.Id && m.User.Id == query.UserId)
                        .Select(m => m.Role)
                        .FirstOrDefault()
                },
                OpponentNickname = d.User1.Id == query.UserId ? d.User2.Nickname : d.User1.Nickname,
                ConfigurationId = d.Configuration != null ? d.Configuration.Id : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
