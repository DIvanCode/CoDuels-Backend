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

public sealed class StartDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class StartDuelSearchHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<StartDuelSearchCommand, Result>
{
    public async Task<Result> Handle(StartDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (duelManager.TryGetWaitingUser(user.Id, out var waitingUser))
        {
            if (waitingUser?.ExpectedOpponentId is null)
            {
                return Result.Ok();
            }

            if (waitingUser.IsOpponentAssigned)
            {
                return Result.Ok();
            }

            duelManager.RemoveUser(user.Id);

            var opponentId = waitingUser.ExpectedOpponentId;
            if (opponentId is not null)
            {
                context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = new SendMessagePayload
                    {
                        UserId = opponentId.Value,
                        Message = new DuelInvitationCanceledMessage
                        {
                            OpponentNickname = user.Nickname,
                            ConfigurationId = waitingUser.ConfigurationId
                        }
                    },
                    RetryUntil = DateTime.UtcNow.AddMinutes(5)
                });

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow);

        return Result.Ok();
    }
}
