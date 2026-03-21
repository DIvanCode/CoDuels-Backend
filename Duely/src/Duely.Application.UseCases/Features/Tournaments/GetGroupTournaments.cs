using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class GetGroupTournamentsQuery : IRequest<Result<List<TournamentDto>>>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
}

public sealed class GetGroupTournamentsHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetGroupTournamentsQuery, Result<List<TournamentDto>>>
{
    private const string Operation = "view tournaments in";

    public async Task<Result<List<TournamentDto>>> Handle(
        GetGroupTournamentsQuery query,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == query.GroupId, cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), query.GroupId);
        }

        var membership = await context.GroupMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                m => m.Group.Id == query.GroupId && m.User.Id == query.UserId,
                cancellationToken);
        if (membership is null || !groupPermissionsService.CanViewGroup(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), query.GroupId);
        }

        var tournaments = await context.Tournaments
            .AsNoTracking()
            .Where(t => t.Group.Id == query.GroupId)
            .Include(t => t.Group)
            .Include(t => t.CreatedBy)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tournaments
            .Select(TournamentDtoMapper.Map)
            .ToList();
    }
}
