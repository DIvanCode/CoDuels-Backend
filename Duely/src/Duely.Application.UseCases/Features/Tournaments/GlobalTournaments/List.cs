using Duely.Application.UseCases.Dto.Tournaments;
using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments.GlobalTournaments;

public sealed class GetGlobalTournamentsQuery : IRequest<Result<List<GlobalTournamentDto>>>
{
    public required Guid UserId { get; init; }
}

public sealed class GetGlobalTournamentsHandler(
    Context context,
    ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
    : IRequestHandler<GetGlobalTournamentsQuery, Result<List<GlobalTournamentDto>>>
{
    public async Task<Result<List<GlobalTournamentDto>>> Handle(
        GetGlobalTournamentsQuery query,
        CancellationToken cancellationToken)
    {
        var userExists = await context.Users.AnyAsync(u => u.Id == query.UserId, cancellationToken);
        if (!userExists)
        {
            return new ForbiddenError();
        }
        
        var tournaments = await context.Tournaments.OfType<GlobalTournament>()
            .AsNoTracking()
            .Include(t => t.Name)
            .Include(t => t.Configuration)
            .ToListAsync(cancellationToken);

        return tournaments
            .Select(tournament => new GlobalTournamentDto
            {
                Id = tournament.Id,
                Name = tournament.Name.Value,
                Type = tournament.Type,
                Status = tournament.Status,
                CreatedBy = new UserShortDto
                {
                    Id = tournament.CreatedBy.Id,
                    Nickname = tournament.CreatedBy.Nickname.Value
                },
                CreatedAt = tournament.CreatedAt,
                Participants = tournament.Participants
                    .Select(p => new UserShortDto
                    {
                        Id = p.User.Id,
                        Nickname = p.User.Nickname.Value
                    })
                    .ToList(),
                Configuration = tournamentConfigurationDtoFactory.Create(tournament.Configuration)
            })
            .ToList();
    }
}
