using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class FinishTournamentCommand : IRequest<Result<TournamentDto>>
{
    public required int UserId { get; init; }
    public required int TournamentId { get; init; }
    public bool IsAdmin { get; init; }
}

public sealed class FinishTournamentHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<FinishTournamentCommand, Result<TournamentDto>>
{
    private const string Operation = "finish tournament in";

    public async Task<Result<TournamentDto>> Handle(
        FinishTournamentCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // This query deliberately has no joins: PostgreSQL can FOR UPDATE the tournament row.
        var lockedId = await context.Tournaments
            .Where(t => t.Id == command.TournamentId)
            .Select(t => t.Id)
            .ForUpdate()
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedId == 0)
        {
            return new EntityNotFoundError(nameof(Tournament), nameof(Tournament.Id), command.TournamentId);
        }

        var tournament = await context.Tournaments
            .Include(t => t.Group)
            .ThenInclude(g => g.Users)
            .ThenInclude(m => m.User)
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .SingleAsync(t => t.Id == command.TournamentId, cancellationToken);
        var membership = tournament.Group.Users.SingleOrDefault(m => m.User.Id == command.UserId);
        if (!command.IsAdmin && (membership is null || !groupPermissionsService.CanFinishTournament(membership)))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), tournament.Group.Id);
        }

        tournament.Status = TournamentStatus.Finished;
        var pendingDuels = await context.PendingDuels
            .OfType<TournamentPendingDuel>()
            .Where(d => d.Tournament.Id == command.TournamentId)
            .ToListAsync(cancellationToken);
        context.PendingDuels.RemoveRange(pendingDuels);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TournamentDtoMapper.Map(tournament);
    }
}
