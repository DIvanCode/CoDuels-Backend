using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetGroupDuelsQuery : IRequest<Result<List<GroupDuelDto>>>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
}

public sealed class GetGroupDuelsHandler(
    Context context,
    IGroupPermissionsService groupPermissionsService,
    IRatingManager ratingManager,
    ITaskService taskService)
    : IRequestHandler<GetGroupDuelsQuery, Result<List<GroupDuelDto>>>
{
    private const string Operation = "view duels in";
    
    public async Task<Result<List<GroupDuelDto>>> Handle(GetGroupDuelsQuery query, CancellationToken cancellationToken)
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
            .Where(m => m.Group.Id == group.Id && m.User.Id == query.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null || !groupPermissionsService.CanViewGroup(membership))
        {
            return new ForbiddenError(nameof(Group), Operation, nameof(Group.Id), query.GroupId);
        }

        var groupWithDuels = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == group.Id)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .Include(g => g.Duels)
            .ThenInclude(d => d.CreatedBy)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .ThenInclude(d => d.Configuration)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .ThenInclude(d => d.User1)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .ThenInclude(d => d.User2)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .ThenInclude(d => d.Winner)
            .Include(g => g.Duels)
            .ThenInclude(d => d.Duel)
            .ThenInclude(d => d.Submissions)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(cancellationToken);
        if (groupWithDuels is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), query.GroupId);
        }

        var groupDuels = groupWithDuels.Duels
            .Select(groupDuel => new
            {
                groupDuel.Duel.StartTime,
                Dto = new GroupDuelDto
                {
                    Duel = DuelDtoMapper.Map(groupDuel.Duel, query.UserId, ratingManager, taskService),
                    User1 = new UserDto
                    {
                        Id = groupDuel.Duel.User1.Id,
                        Nickname = groupDuel.Duel.User1.Nickname,
                        Rating = groupDuel.Duel.User1InitRating,
                        CreatedAt = groupDuel.Duel.User1.CreatedAt
                    },
                    User2 = new UserDto
                    {
                        Id = groupDuel.Duel.User2.Id,
                        Nickname = groupDuel.Duel.User2.Nickname,
                        Rating = groupDuel.Duel.User2InitRating,
                        CreatedAt = groupDuel.Duel.User2.CreatedAt
                    },
                    IsAcceptedByUser1 = true,
                    IsAcceptedByUser2 = true,
                    CreatedBy = new UserDto
                    {
                        Id = groupDuel.CreatedBy.Id,
                        Nickname = groupDuel.CreatedBy.Nickname,
                        Rating = groupDuel.CreatedBy.Rating,
                        CreatedAt = groupDuel.CreatedBy.CreatedAt
                    },
                    CreatedAt = groupDuel.Duel.StartTime,
                    ConfigurationId = groupDuel.Duel.Configuration.Id
                }
            });

        var pendingDuels = await context.PendingDuels.OfType<GroupPendingDuel>()
            .AsNoTracking()
            .Where(d => d.Group.Id == group.Id)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.CreatedBy)
            .Select(d => new
            {
                StartTime = d.CreatedAt,
                Dto = new GroupDuelDto
                {
                    Duel = null,
                    User1 = new UserDto
                    {
                        Id = d.User1.Id,
                        Nickname = d.User1.Nickname,
                        Rating = d.User1.Rating,
                        CreatedAt = d.User1.CreatedAt
                    },
                    User2 = new UserDto
                    {
                        Id = d.User2.Id,
                        Nickname = d.User2.Nickname,
                        Rating = d.User2.Rating,
                        CreatedAt = d.User2.CreatedAt
                    },
                    IsAcceptedByUser1 = d.IsAcceptedByUser1,
                    IsAcceptedByUser2 = d.IsAcceptedByUser2,
                    CreatedBy = new UserDto
                    {
                        Id = d.CreatedBy.Id,
                        Nickname = d.CreatedBy.Nickname,
                        Rating = d.CreatedBy.Rating,
                        CreatedAt = d.CreatedBy.CreatedAt
                    },
                    CreatedAt = d.CreatedAt,
                    ConfigurationId = d.Configuration != null ? d.Configuration.Id : null
                }
            })
            .ToListAsync(cancellationToken);

        return groupDuels
            .Concat(pendingDuels)
            .OrderByDescending(x => x.StartTime)
            .Select(x => x.Dto)
            .ToList();
    }
}
