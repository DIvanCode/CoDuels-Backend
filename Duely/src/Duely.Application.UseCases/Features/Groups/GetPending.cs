using Duely.Application.UseCases.Dto.Groups;
using Duely.Domain.Common.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetPendingGroupsQuery : IRequest<Result<List<GroupShortDto>>>
{
    public required Guid UserId { get; init; }
}

internal sealed class GetPendingGroupsHandler(Context context)
    : IRequestHandler<GetPendingGroupsQuery, Result<List<GroupShortDto>>>
{
    public async Task<Result<List<GroupShortDto>>> Handle(
        GetPendingGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var groups = await context.Groups
            .Include(g => g.Memberships)
            .ThenInclude(m => m.User)
            .Where(g => g.Memberships.Any(m => m.User.Id == query.UserId && !m.IsConfirmed))
            .ToListAsync(cancellationToken);
        
        return groups
            .Select(g =>
            {
                var membership = g.GetMembership(user)!;
                return new GroupShortDto
                {
                    Id = g.Id,
                    Name = g.Name.Value,
                    Membership = new GroupMembershipShortDto
                    {
                        Role = membership.Role,
                        IsConfirmed = membership.IsConfirmed
                    }
                };
            })
            .ToList();
    }
}
