using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models;
using Duely.Domain.Models.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class ChangeGlobalTournamentRegistrationCommand : IRequest<Result<TournamentDto>>
{
    public required int TournamentId { get; init; }
    public required int UserId { get; init; }
    public required bool Join { get; init; }
}

public sealed class ChangeGlobalTournamentRegistrationHandler(Context context)
    : IRequestHandler<ChangeGlobalTournamentRegistrationCommand, Result<TournamentDto>>
{
    public async Task<Result<TournamentDto>> Handle(
        ChangeGlobalTournamentRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var tournament = await context.Tournaments
            .OfType<SwissTournament>()
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .SingleOrDefaultAsync(t => t.Id == command.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return new EntityNotFoundError(nameof(SwissTournament), nameof(Tournament.Id), command.TournamentId);
        }

        if (tournament.Status != TournamentStatus.New ||
            tournament.StartsAt is null ||
            tournament.StartsAt <= DateTime.UtcNow)
        {
            return new ForbiddenError(nameof(Tournament), "change registration for", nameof(Tournament.Id), command.TournamentId);
        }

        var participant = tournament.Participants.SingleOrDefault(p => p.User.Id == command.UserId);
        if (command.Join)
        {
            if (participant is not null)
            {
                return TournamentDtoMapper.Map(tournament);
            }

            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
            if (user is null)
            {
                return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
            }

            tournament.Participants.Add(new TournamentParticipant
            {
                Tournament = tournament,
                User = user,
                Seed = tournament.Participants.Count == 0 ? 1 : tournament.Participants.Max(p => p.Seed) + 1
            });
        }
        else
        {
            if (participant is null)
            {
                return TournamentDtoMapper.Map(tournament);
            }

            tournament.Participants.Remove(participant);
        }

        tournament.RegistrationRevision++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new EntityAlreadyExistsError("Tournament registration changed concurrently. Retry the request.");
        }
        catch (DbUpdateException error)
            when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new EntityAlreadyExistsError("Tournament registration changed concurrently. Retry the request.");
        }

        return TournamentDtoMapper.Map(tournament);
    }
}
