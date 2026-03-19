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

public sealed class StartTournamentCommand : IRequest<Result<TournamentDto>>
{
    public required int UserId { get; init; }
    public required int TournamentId { get; init; }
}

public sealed class StartTournamentHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<StartTournamentCommand, Result<TournamentDto>>
{
    private const string Operation = "start tournament in";

    public async Task<Result<TournamentDto>> Handle(StartTournamentCommand command, CancellationToken cancellationToken)
    {
        var tournament = await context.Tournaments
            .Include(t => t.Group)
            .ThenInclude(g => g.Users)
            .ThenInclude(m => m.User)
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .SingleOrDefaultAsync(t => t.Id == command.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return new EntityNotFoundError(nameof(Tournament), nameof(Tournament.Id), command.TournamentId);
        }

        var membership = tournament.Group.Users.SingleOrDefault(m => m.User.Id == command.UserId);
        if (membership is null || !groupPermissionsService.CanStartTournament(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), tournament.Group.Id);
        }

        if (tournament.Status == TournamentStatus.New)
        {
            tournament.Status = TournamentStatus.InProgress;
            await context.SaveChangesAsync(cancellationToken);
        }

        return TournamentDtoMapper.Map(tournament);
    }
}
