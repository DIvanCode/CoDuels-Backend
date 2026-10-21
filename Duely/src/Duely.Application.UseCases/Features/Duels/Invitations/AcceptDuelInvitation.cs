using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class AcceptDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class AcceptDuelInvitationHandler(Context context)
    : IRequestHandler<AcceptDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(AcceptDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        
        var opponent = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
        if (opponent is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        var friendlyPendingDuel = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.Configuration)
            .Where(d => d.User1.Id == opponent.Id && d.User2.Id == user.Id)
            .OrderBy(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (friendlyPendingDuel is null)
        {
            return new EntityNotFoundError(nameof(FriendlyPendingDuel), nameof(User.Id), command.UserId);
        }

        friendlyPendingDuel.IsAccepted = true;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
