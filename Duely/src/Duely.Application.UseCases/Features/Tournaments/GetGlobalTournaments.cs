using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class GetGlobalTournamentsQuery : IRequest<Result<List<TournamentDto>>>
{
}

public sealed class GetGlobalTournamentsHandler(Context context)
    : IRequestHandler<GetGlobalTournamentsQuery, Result<List<TournamentDto>>>
{
    public async Task<Result<List<TournamentDto>>> Handle(
        GetGlobalTournamentsQuery query,
        CancellationToken cancellationToken)
    {
        var tournaments = await context.Tournaments
            .AsNoTracking()
            .OfType<SwissTournament>()
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .OrderByDescending(t => t.StartsAt)
            .ToListAsync(cancellationToken);

        return tournaments.Select(TournamentDtoMapper.Map).ToList();
    }
}
