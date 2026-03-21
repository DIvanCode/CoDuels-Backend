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

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class AcceptTournamentDuelCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int TournamentId { get; init; }
}

public sealed class AcceptTournamentDuelHandler(Context context)
    : IRequestHandler<AcceptTournamentDuelCommand, Result>
{
    public async Task<Result> Handle(AcceptTournamentDuelCommand command, CancellationToken cancellationToken)
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

        var tournamentPendingDuel = await context.PendingDuels.OfType<TournamentPendingDuel>()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(d =>
                d.Tournament.Id == command.TournamentId &&
                (d.User1.Id == command.UserId || d.User2.Id == command.UserId),
                cancellationToken);
        if (tournamentPendingDuel is null)
        {
            return new EntityNotFoundError(nameof(TournamentPendingDuel), nameof(User.Id), command.UserId);
        }

        if (tournamentPendingDuel.User1.Id == command.UserId)
        {
            tournamentPendingDuel.IsAcceptedByUser1 = true;
        }
        else
        {
            tournamentPendingDuel.IsAcceptedByUser2 = true;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
