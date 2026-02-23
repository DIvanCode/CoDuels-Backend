using Duely.Application.UseCases.Dtos;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetUserGroupsQuery : IRequest<Result<List<GroupDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetUserGroupsHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetUserGroupsQuery, Result<List<GroupDto>>>
{
    public async Task<Result<List<GroupDto>>> Handle(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        var memberships = await context.GroupMemberships
            .AsNoTracking()
            .Include(m => m.Group)
            .Where(m => m.User.Id == query.UserId)
            .ToListAsync(cancellationToken);

        return memberships
            .Where(groupPermissionsService.CanViewGroup)
            .Select(m => new GroupDto
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                UserRole = m.Role
            })
            .ToList();
    }
}
