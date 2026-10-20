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
        
        DuelConfiguration? configuration = null;
        if (command.ConfigurationId is not null)
        {
            configuration = await context.DuelConfigurations
                .SingleOrDefaultAsync(c => c.Id == command.ConfigurationId, cancellationToken);
            if (configuration is null)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.ConfigurationId.Value);
            }
        }

        var alreadyInvited = await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .AnyAsync(d => d.User1.Id == user.Id && d.User2.Id == opponent.Id &&
                ((command.ConfigurationId == null && d.Configuration == null) ||
                 (d.Configuration != null && d.Configuration.Id == command.ConfigurationId)),
                cancellationToken);
        if (alreadyInvited)
        {
            return Result.Ok();
        }

        var outgoingFriendlyPendingDuel = new FriendlyPendingDuel
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
