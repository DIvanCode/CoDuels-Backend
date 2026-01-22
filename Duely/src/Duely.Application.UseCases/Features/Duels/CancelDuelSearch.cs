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

public sealed class CancelDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class CancelDuelSearchHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<CancelDuelSearchCommand, Result>
{
    public async Task<Result> Handle(CancelDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (duelManager.TryGetWaitingUser(user.Id, out var waitingUser) &&
            waitingUser?.ExpectedOpponentId is null)
        {
            duelManager.RemoveUser(user.Id);

            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = user.Id,
                    Message = new DuelSearchCanceledMessage()
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok();
    }
}
