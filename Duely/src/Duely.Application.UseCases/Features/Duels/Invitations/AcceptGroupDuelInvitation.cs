using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class AcceptGroupDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class AcceptGroupDuelInvitationHandler(Context context)
    : IRequestHandler<AcceptGroupDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(AcceptGroupDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        
        var groupPendingDuel = await context.PendingDuels.OfType<GroupPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d =>
                d.Group.Id == command.GroupId &&
                ((d.User1.Id == command.UserId && d.User2.Nickname == command.OpponentNickname) ||
                 (d.User2.Id == command.UserId && d.User1.Nickname == command.OpponentNickname)) &&
                ((command.ConfigurationId == null && d.Configuration == null) ||
                 (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (groupPendingDuel is null)
        {
            return new EntityNotFoundError(nameof(GroupPendingDuel), nameof(User.Id), command.UserId);
        }

        if (groupPendingDuel.User1.Id == command.UserId)
        {
            groupPendingDuel.IsAcceptedByUser1 = true;
        }
        else
        {
            groupPendingDuel.IsAcceptedByUser2 = true;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
