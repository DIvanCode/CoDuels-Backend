using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class AcceptDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class AcceptDuelInvitationHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<AcceptDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(AcceptDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var inviter = await context.Users
            .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
        if (inviter is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        if (inviter.Id == user.Id)
        {
            return new ForbiddenError(nameof(User), "accept invitation", nameof(User.Id), user.Id);
        }

        if (!duelManager.TryGetWaitingUser(inviter.Id, out var invitation) ||
            invitation?.ExpectedOpponentId != user.Id ||
            (command.ConfigurationId is not null && invitation.ConfigurationId != command.ConfigurationId))
        {
            return new EntityNotFoundError(nameof(User), "invitation from", inviter.Nickname);
        }

        if (duelManager.TryGetWaitingUser(user.Id, out var waitingUser))
        {
            if (waitingUser?.ExpectedOpponentId is null)
            {
                duelManager.RemoveUser(user.Id);
            }
            else if (waitingUser.ExpectedOpponentId != inviter.Id)
            {
                return new EntityAlreadyExistsError(nameof(User), nameof(User.Id), user.Id);
            }
            else
            {
                return Result.Ok();
            }
        }

        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow, inviter.Id, invitation.ConfigurationId);

        return Result.Ok();
    }
}
