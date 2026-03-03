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

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CancelPendingDuelsCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class CancelPendingDuelsHandler(Context context) : IRequestHandler<CancelPendingDuelsCommand, Result>
{
    public async Task<Result> Handle(CancelPendingDuelsCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var rankedPendingDuels = await context.PendingDuels.OfType<RankedPendingDuel>()
            .Where(d => d.User.Id == command.UserId)
            .ToListAsync(cancellationToken);
        if (rankedPendingDuels.Count > 0)
        {
            context.PendingDuels.RemoveRange(rankedPendingDuels);
        }

        var outgoingFriendlyDuels = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Configuration)
            .Where(d => d.User1.Id == command.UserId)
            .ToListAsync(cancellationToken);
        foreach (var duel in outgoingFriendlyDuels)
        {
            context.PendingDuels.Remove(duel);

            context.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = new SendMessagePayload
                    {
                        UserId = duel.User1.Id,
                        Message = new DuelInvitationCanceledMessage
                        {
                            OpponentNickname = duel.User2.Nickname,
                            ConfigurationId = duel.Configuration?.Id
                        }
                    },
                    RetryUntil = DateTime.UtcNow.AddMinutes(5)
                },
                new OutboxMessage
                {
                    Type = OutboxType.SendMessage,
                    Payload = new SendMessagePayload
                    {
                        UserId = duel.User2.Id,
                        Message = new DuelInvitationCanceledMessage
                        {
                            OpponentNickname = duel.User1.Nickname,
                            ConfigurationId = duel.Configuration?.Id
                        }
                    },
                    RetryUntil = DateTime.UtcNow.AddMinutes(5)
                });
        }

        var incomingFriendlyDuels = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.User2)
            .Where(d => d.User2.Id == command.UserId)
            .ToListAsync(cancellationToken);
        foreach (var duel in incomingFriendlyDuels)
        {
            duel.IsAccepted = false;
        }

        var groupPendingDuels = await context.PendingDuels.OfType<GroupPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Where(d => d.User1.Id == command.UserId || d.User2.Id == command.UserId)
            .ToListAsync(cancellationToken);
        foreach (var duel in groupPendingDuels)
        {
            if (duel.User1.Id == command.UserId)
            {
                duel.IsAcceptedByUser1 = false;
            }
            else
            {
                duel.IsAcceptedByUser2 = false;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
