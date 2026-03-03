using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetGroupUsersQuery : IRequest<Result<List<GroupUserDto>>>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
}

public sealed class GetGroupUsersHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetGroupUsersQuery, Result<List<GroupUserDto>>>
{
    private const string Operation = "view users of";
    
    public async Task<Result<List<GroupUserDto>>> Handle(GetGroupUsersQuery query, CancellationToken cancellationToken)
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

        var users = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.Group.Id == group.Id)
            .Include(m => m.User)
            .Include(m => m.InvitedBy)
            .ToListAsync(cancellationToken);

        return users
            .Select(m => new GroupUserDto
            {
                User = new UserDto
                {
                    Id = m.User.Id,
                    Nickname = m.User.Nickname,
                    Rating = m.User.Rating,
                    CreatedAt = m.User.CreatedAt
                },
                Role = m.Role,
                Status = m.InvitationPending ? GroupUserStatus.Pending : GroupUserStatus.Active,
                InvitedBy = m.InvitedBy is null
                    ? null
                    : new UserDto
                    {
                        Id = m.InvitedBy.Id,
                        Nickname = m.InvitedBy.Nickname,
                        Rating = m.InvitedBy.Rating,
                        CreatedAt = m.InvitedBy.CreatedAt
                    }
            })
            .ToList();;
    }
}
