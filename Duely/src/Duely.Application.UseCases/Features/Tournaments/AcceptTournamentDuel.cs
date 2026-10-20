using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
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
