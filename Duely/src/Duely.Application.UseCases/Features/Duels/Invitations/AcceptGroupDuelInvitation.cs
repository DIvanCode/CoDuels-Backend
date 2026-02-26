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

public sealed class AcceptGroupDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class AcceptGroupDuelInvitationHandler(Context context)
    : IRequestHandler<AcceptGroupDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(AcceptGroupDuelInvitationCommand command, CancellationToken cancellationToken)
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
        
        var groupPendingDuel = await context.PendingDuels.OfType<GroupPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d =>
                d.Group.Id == command.GroupId &&
                ((d.User1.Id == command.UserId && d.User2.Nickname == command.OpponentNickname) ||
                 (d.User2.Id == command.UserId && d.User1.Nickname == command.OpponentNickname)) &&
                ((command.ConfigurationId == null && d.Configuration == null) ||
                 (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (groupPendingDuel is null)
        {
            return new EntityNotFoundError(nameof(GroupPendingDuel), nameof(User.Id), command.UserId);
        }

        if (groupPendingDuel.User1.Id == command.UserId)
        {
            groupPendingDuel.IsAcceptedByUser1 = true;
        }
        else
        {
            groupPendingDuel.IsAcceptedByUser2 = true;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
