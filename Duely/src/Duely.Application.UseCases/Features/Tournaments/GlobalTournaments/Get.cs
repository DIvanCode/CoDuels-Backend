using Duely.Application.UseCases.Dto.Tournaments;
using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Duely.Domain.Models.Tournaments.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments.GlobalTournaments;

public sealed class GetGlobalTournamentQuery : IRequest<Result<GlobalTournamentDto>>
{
    public required Guid UserId { get; init; }
    public required Guid TournamentId { get; init; }
}

public sealed class GetGlobalTournamentHandler(
    Context context,
    ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
    : IRequestHandler<GetGlobalTournamentQuery, Result<GlobalTournamentDto>>
{
    public async Task<Result<GlobalTournamentDto>> Handle(
        GetGlobalTournamentQuery query,
        CancellationToken cancellationToken)
    {
        var userExists = await context.Users.AnyAsync(u => u.Id == query.UserId, cancellationToken);
        if (!userExists)
        {
            return new ForbiddenError();
        }
        
        var tournament = await context.Tournaments.OfType<GlobalTournament>()
            .AsNoTracking()
            .Include(t => t.Name)
            .Include(t => t.Configuration)
            .SingleOrDefaultAsync(t => t.Id == query.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return new TournamentNotFoundError();
        }

        return new GlobalTournamentDto
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
        };
    }
}
