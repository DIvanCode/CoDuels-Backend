using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Search;

public sealed class StartDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class StartDuelSearchHandler(Context context) : IRequestHandler<StartDuelSearchCommand, Result>
{
    public async Task<Result> Handle(StartDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var activeDuel = await context.Duels
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                    d.Status == DuelStatus.InProgress &&
                    (d.User1.Id == command.UserId || d.User2.Id == command.UserId),
                cancellationToken);
        if (activeDuel is not null)
        {
            return new EntityAlreadyExistsError(nameof(Duel), nameof(User.Id), command.UserId);
        }
        
        var outgoingFriendlyPendingDuel = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.User2)
            .Include(d => d.Configuration)
            .SingleOrDefaultAsync(d => d.User1.Id == command.UserId, cancellationToken);
        if (outgoingFriendlyPendingDuel is not null)
        {
            context.PendingDuels.Remove(outgoingFriendlyPendingDuel);
            
            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = outgoingFriendlyPendingDuel.User2.Id,
                    Message = new DuelInvitationCanceledMessage
                    {
                        OpponentNickname = user.Nickname,
                        ConfigurationId = outgoingFriendlyPendingDuel.Configuration?.Id
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });
            
            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = user.Id,
                    Message = new DuelInvitationCanceledMessage
                    {
                        OpponentNickname = outgoingFriendlyPendingDuel.User2.Nickname,
                        ConfigurationId = outgoingFriendlyPendingDuel.Configuration?.Id
                    }
                },
                RetryUntil = DateTime.UtcNow.AddMinutes(5)
            });
        }

        var rankedPendingDuel = await context.PendingDuels.OfType<RankedPendingDuel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.User.Id == command.UserId, cancellationToken);
        if (rankedPendingDuel is not null)
        {
            return Result.Ok();
        }

        rankedPendingDuel = new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = user,
            Rating = user.Rating,
            CreatedAt = DateTime.UtcNow
        };
        context.PendingDuels.Add(rankedPendingDuel);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
