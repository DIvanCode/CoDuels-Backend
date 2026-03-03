using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Invitations;

public sealed class CancelDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class CancelDuelInvitationHandler(Context context)
    : IRequestHandler<CancelDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(CancelDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
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

        var friendlyPendingDuel = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.Configuration)
            .SingleOrDefaultAsync(d =>
                    d.User1.Id == user.Id && d.User2.Id == opponent.Id &&
                    ((command.ConfigurationId == null && d.Configuration == null) ||
                     (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (friendlyPendingDuel is null)
        {
            return Result.Ok();
        }

        context.PendingDuels.Remove(friendlyPendingDuel);

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
                        ConfigurationId = command.ConfigurationId
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
                        ConfigurationId = command.ConfigurationId
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
