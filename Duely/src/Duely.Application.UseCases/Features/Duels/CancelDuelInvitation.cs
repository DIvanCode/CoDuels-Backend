using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CancelDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class CancelDuelInvitationHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<CancelDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(CancelDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var opponent = await context.Users
            .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
        if (opponent is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        if (opponent.Id == user.Id)
        {
            return new ForbiddenError(nameof(User), "cancel invitation", nameof(User.Id), user.Id);
        }

        if (!duelManager.TryGetWaitingUser(user.Id, out var waitingUser) ||
            waitingUser?.ExpectedOpponentId != opponent.Id ||
            (command.ConfigurationId is not null && waitingUser.ConfigurationId != command.ConfigurationId))
        {
            return Result.Ok();
        }

        duelManager.RemoveUser(user.Id);

        context.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = opponent.Id,
                    Message = new DuelInvitationCanceledMessage
                    {
                        OpponentNickname = user.Nickname,
                        ConfigurationId = waitingUser.ConfigurationId
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            },
            new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = user.Id,
                    Message = new DuelInvitationCanceledMessage
                    {
                        OpponentNickname = opponent.Nickname,
                        ConfigurationId = waitingUser.ConfigurationId
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
