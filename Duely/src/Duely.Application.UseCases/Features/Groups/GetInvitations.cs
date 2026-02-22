using Duely.Application.UseCases.Dtos;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetGroupInvitationsQuery : IRequest<Result<List<GroupInvitationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetGroupInvitationsHandler(Context context)
    : IRequestHandler<GetGroupInvitationsQuery, Result<List<GroupInvitationDto>>>
{
    public async Task<Result<List<GroupInvitationDto>>> Handle(
        GetGroupInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var memberships = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.User.Id == query.UserId && m.InvitationPending)
            .Include(m => m.Group)
            .ToListAsync(cancellationToken);
        
        return memberships
            .Select(m => new GroupInvitationDto
            {
                GroupId = m.Group.Id,
                GroupName = m.Group.Name,
                Role = m.Role
            })
            .ToList();;
    }
}
