using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class GetTournamentQuery : IRequest<Result<TournamentDetailsDto>>
{
    public required int UserId { get; init; }
    public required int TournamentId { get; init; }
}

public sealed class GetTournamentHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    ITournamentDetailsMapperResolver tournamentDetailsMapperResolver)
    : IRequestHandler<GetTournamentQuery, Result<TournamentDetailsDto>>
{
    private const string Operation = "view tournament in";

    public async Task<Result<TournamentDetailsDto>> Handle(GetTournamentQuery query, CancellationToken cancellationToken)
    {
        var tournament = await context.Tournaments
            .AsNoTracking()
            .Include(t => t.Group)
            .ThenInclude(g => g.Users)
            .ThenInclude(m => m.User)
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .SingleOrDefaultAsync(t => t.Id == query.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return new EntityNotFoundError(nameof(Tournament), nameof(Tournament.Id), query.TournamentId);
        }

        var membership = tournament.Group.Users.SingleOrDefault(m => m.User.Id == query.UserId);
        if (membership is null || !groupPermissionsService.CanViewGroup(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), tournament.Group.Id);
        }

        var detailsMapper = tournamentDetailsMapperResolver.GetMapper(tournament.MatchmakingType);
        var userIds = detailsMapper.GetReferencedUserIds(tournament);

        var usersById = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var duelIds = detailsMapper.GetReferencedDuelIds(tournament);

        var duelsById = await context.Duels
            .AsNoTracking()
            .Where(d => duelIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        return detailsMapper.MapDetails(tournament, usersById, duelsById);
    }
}
