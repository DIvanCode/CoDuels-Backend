using Duely.Application.UseCases.Dto.Tournaments;
using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Duely.Domain.Models.Tournaments.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Tournaments.GlobalTournaments;

public sealed class StartGlobalTournamentCommand : IRequest<Result<GlobalTournamentDto>>
{
    public required Guid UserId { get; init; }
    public required Guid TournamentId { get; init; }
}

public sealed class StartGlobalTournamentHandler(
    Context context,
    ILogger<StartGlobalTournamentHandler> logger,
    ITournamentConfigurationDtoFactory tournamentConfigurationDtoFactory)
    : IRequestHandler<StartGlobalTournamentCommand, Result<GlobalTournamentDto>>
{
    public async Task<Result<GlobalTournamentDto>> Handle(
        StartGlobalTournamentCommand command,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var tournament = await context.Tournaments.OfType<GlobalTournament>()
            .Include(t => t.Name)
            .Include(t => t.Configuration)
            .SingleOrDefaultAsync(t => t.Id == command.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return new TournamentNotFoundError();
        }
        
        if (!user.IsAdmin)
        {
            return new ForbiddenError();
        }

        if (tournament.Status != TournamentStatus.New)
        {
            return new ForbiddenError("Нельзя запустить уже запущенный турнир.");
        }

        tournament.Start();
        
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {Nickname} started global tournament {TournamentId}",
            user.Nickname, tournament.Id);

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
