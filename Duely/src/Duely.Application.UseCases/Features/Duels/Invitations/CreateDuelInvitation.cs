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

public sealed class CreateDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class CreateDuelInvitationHandler(Context context)
    : IRequestHandler<CreateDuelInvitationCommand, Result>
{
    private const string Operation = "invite to duel";
    
    public async Task<Result> Handle(CreateDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var activeDuel = await context.Duels
            .AsNoTracking()
            .SingleOrDefaultAsync(
                d => d.Status == DuelStatus.InProgress &&
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
            if (outgoingFriendlyPendingDuel.User2.Nickname == command.OpponentNickname)
            {
                return Result.Ok();
            }
            
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
        }

        var opponent = await context.Users
            .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
        if (opponent is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
        }

        if (opponent.Id == user.Id)
        {
            return new ForbiddenError(nameof(User), Operation, nameof(User.Id), user.Id);
        }
        
        var opponentActiveDuel = await context.Duels
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                    d.Status == DuelStatus.InProgress &&
                    (d.User1.Nickname == command.OpponentNickname || d.User2.Nickname == command.OpponentNickname),
                cancellationToken);
        if (opponentActiveDuel is not null)
        {
            return new EntityAlreadyExistsError(nameof(Duel), nameof(User.Nickname), command.OpponentNickname);
        }

        DuelConfiguration? configuration = null;
        if (command.ConfigurationId is not null)
        {
            configuration = await context.DuelConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == command.ConfigurationId, cancellationToken);
            if (configuration is null)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.ConfigurationId.Value);
            }
        }

        outgoingFriendlyPendingDuel = new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = user,
            User2 = opponent,
            Configuration = configuration,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        };
        context.PendingDuels.Add(outgoingFriendlyPendingDuel);

        context.OutboxMessages.Add(new OutboxMessage
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

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
