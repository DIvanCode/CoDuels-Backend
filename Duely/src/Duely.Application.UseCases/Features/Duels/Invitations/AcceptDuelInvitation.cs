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
        
        var rankedPendingDuel = await context.PendingDuels.OfType<RankedPendingDuel>()
            .SingleOrDefaultAsync(d => d.User.Id == command.UserId, cancellationToken);
        if (rankedPendingDuel is not null)
        {
            context.PendingDuels.Remove(rankedPendingDuel);

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

        var opponent = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
        if (opponent is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        var friendlyPendingDuel = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.Configuration)
            .SingleOrDefaultAsync(d =>
                    d.User1.Id == opponent.Id && d.User2.Id == user.Id &&
                    ((command.ConfigurationId == null && d.Configuration == null) ||
                     (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (friendlyPendingDuel is null)
        {
            return new EntityNotFoundError(nameof(FriendlyPendingDuel), nameof(User.Id), command.UserId);
        }

        friendlyPendingDuel.IsAccepted = true;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
