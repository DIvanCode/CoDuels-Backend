using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Groups;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class GetDuelHandler(
    Context context,
    IRatingManager ratingManager,
    ITaskService taskService,
    IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .AsNoTracking()
            .Where(d => d.Id == query.DuelId)
            .Include(d => d.Configuration)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .Include(d => d.Submissions)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }

        var isParticipant = duel.User1.Id == query.UserId || duel.User2.Id == query.UserId;
        var isViewer = false;

        if (!isParticipant)
        {
            var groupDuel = await context.GroupDuels
                .AsNoTracking()
                .Include(d => d.Group)
                .Where(d => d.Duel.Id == query.DuelId)
                .SingleOrDefaultAsync(cancellationToken);

            if (groupDuel is not null)
            {
                var membership = await context.GroupMemberships
                    .AsNoTracking()
                    .Where(m => m.Group.Id == groupDuel.Group.Id && m.User.Id == query.UserId)
                    .SingleOrDefaultAsync(cancellationToken);

                isViewer = membership is not null && groupPermissionsService.CanViewDuel(membership);
            }
        }

        if (!isParticipant && !isViewer)
        {
            return new ForbiddenError(nameof(Duel), "get", nameof(Duel.Id), query.DuelId);  
        }

        return DuelDtoMapper.Map(duel, query.UserId, isViewer, ratingManager, taskService);
    }
}
