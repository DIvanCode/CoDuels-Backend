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

public sealed class DenyDuelInvitationCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required string OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
}

public sealed class DenyDuelInvitationHandler(Context context)
    : IRequestHandler<DenyDuelInvitationCommand, Result>
{
    public async Task<Result> Handle(DenyDuelInvitationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
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

        context.PendingDuels.Remove(friendlyPendingDuel);

        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = opponent.Id,
                Message = new DuelInvitationDeniedMessage
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
