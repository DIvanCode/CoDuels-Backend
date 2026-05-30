using Duely.Application.UseCases.Dto.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetUserGroupsQuery : IRequest<Result<List<GroupShortDto>>>
{
    public required Guid UserId { get; init; }
}

public sealed class GetUserGroupsHandler(Context context)
    : IRequestHandler<GetUserGroupsQuery, Result<List<GroupShortDto>>>
{
    public async Task<Result<List<GroupShortDto>>> Handle(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        var memberships = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.User.Id == query.UserId)
            .Include(m => m.Group)
                .ThenInclude(g => g.Name)
            .ToListAsync(cancellationToken);
        return memberships
            .Select(m => new GroupShortDto
            {
                Id = m.Group.Id,
                Name = m.Group.Name.Value,
                Membership = new GroupMembershipShortDto
                {
                    Role = m.Role,
                    IsConfirmed = m.IsConfirmed
                }
            })
            .ToList();
    }
}
