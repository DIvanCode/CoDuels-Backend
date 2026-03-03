using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Groups;
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
    private const string Operation = "view";
    
    public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
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

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            UserRole = membership.Role
        };
    }
}
