using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class DenyDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
}

public sealed class DenyDuelInvitationHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<DenyDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(DenyDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var inviter = await context.Users.SingleOrDefaultAsync(
            u => u.Nickname == command.OpponentNickname,
            cancellationToken);
        if (inviter is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        if (inviter.Id == user.Id)
        {
            return new ForbiddenError(nameof(User), "deny invitation", nameof(User.Id), user.Id);
        }

        if (!duelManager.TryRemoveInvitation(inviter.Id, user.Id))
        {
            return new EntityNotFoundError(nameof(User), "invitation from", inviter.Nickname);
        }

        var payload = JsonSerializer.Serialize(
            new SendMessagePayload(inviter.Id, MessageType.DuelCanceled, 0, user.Nickname)
        );

        context.Outbox.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = payload,
            Status = OutboxStatus.ToDo,
            Retries = 0,
            RetryAt = null,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
