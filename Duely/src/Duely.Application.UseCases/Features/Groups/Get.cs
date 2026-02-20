using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetGroupQuery : IRequest<Result<GroupDto>>
{
    public required int UserId { get; init; }
    public required int GroupId { get; init; }
}

public sealed class GetGroupHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetGroupQuery, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == query.GroupId)
            .Include(g => g.Users.Where(u => u.User.Id == query.UserId))
            .SingleOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return new EntityNotFoundError(nameof(Group), nameof(Group.Id), query.GroupId);
        }

        var userGroupRole = group.Users.SingleOrDefault();
        if (userGroupRole is null || !groupPermissionsService.HasReadPermission(userGroupRole.Role))
        {
            return new ForbiddenError(nameof(Group), "read", nameof(Group.Id), query.GroupId);
        }

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name
        };
    }
}
