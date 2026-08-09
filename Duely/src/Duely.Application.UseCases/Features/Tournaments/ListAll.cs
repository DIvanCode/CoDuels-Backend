using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class GetTournamentsByStateQuery : IRequest<Result<List<TournamentDto>>>
{
    public required bool Finished { get; init; }
}

public sealed class GetTournamentsByStateHandler(Context context)
    : IRequestHandler<GetTournamentsByStateQuery, Result<List<TournamentDto>>>
{
    public async Task<Result<List<TournamentDto>>> Handle(
        GetTournamentsByStateQuery query,
        CancellationToken cancellationToken)
    {
        var tournaments = context.Tournaments.AsNoTracking();
        tournaments = query.Finished
            ? tournaments.Where(tournament => tournament.Status == TournamentStatus.Finished)
            : tournaments.Where(tournament => tournament.Status != TournamentStatus.Finished);

        var result = await tournaments
            .Include(tournament => tournament.Group)
            .Include(tournament => tournament.CreatedBy)
            .Include(tournament => tournament.DuelConfiguration)
            .Include(tournament => tournament.Participants)
            .ThenInclude(participant => participant.User)
            .OrderByDescending(tournament => tournament.CreatedAt)
            .ToListAsync(cancellationToken);

        return result.Select(TournamentDtoMapper.Map).ToList();
    }
}
