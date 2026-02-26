using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CreateDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class CreateDuelInvitationHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<CreateDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(CreateDuelInvitationCommand command, CancellationToken cancellationToken)
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
            return new ForbiddenError(nameof(User), "invitation to duel", nameof(User.Id), user.Id);
        }

        if (command.ConfigurationId is not null)
        {
            var configurationExists = await context.DuelConfigurations
                .AnyAsync(c => c.Id == command.ConfigurationId, cancellationToken);
            if (!configurationExists)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.ConfigurationId.Value);
            }
        }

        if (duelManager.TryGetWaitingUser(opponent.Id, out var waitingOpponent) &&
            waitingOpponent?.ExpectedOpponentId == user.Id)
        {
            return new EntityAlreadyExistsError("DuelInvitation", $"from '{opponent.Nickname}' to '{user.Nickname}'");
        }

        var outboxMessages = new List<OutboxMessage>();

        if (duelManager.TryGetWaitingUser(user.Id, out var waitingUser))
        {
            if (waitingUser?.ExpectedOpponentId is null)
            {
                duelManager.RemoveUser(user.Id);
            }
            else
            {
                duelManager.RemoveUser(user.Id);

                if (waitingUser.ExpectedOpponentId is not null)
                {
                    outboxMessages.Add(new OutboxMessage
                    {
                        Type = OutboxType.SendMessage,
                        Payload = new SendMessagePayload
                        {
                            UserId = waitingUser.ExpectedOpponentId.Value,
                            Message = new DuelInvitationCanceledMessage
                            {
                                OpponentNickname = user.Nickname,
                                ConfigurationId = waitingUser.ConfigurationId
                            }
                        },
                        RetryUntil = DateTime.UtcNow.AddMinutes(5)
                    });
                }
            }
        }

        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow, opponent.Id, command.ConfigurationId);

        outboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = opponent.Id,
                Message = new DuelInvitationMessage
                {
                    OpponentNickname = user.Nickname,
                    ConfigurationId = command.ConfigurationId
                }
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });

        context.OutboxMessages.AddRange(outboxMessages);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
