using Duely.Application.UseCases.Dto.Groups;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Groups.Errors;
using Duely.Domain.Services.Groups;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetGroupQuery : IRequest<Result<GroupDto>>
{
    public required Guid UserId { get; init; }
    public required Guid GroupId { get; init; }
}

public sealed class GetGroupHandler(Context context, IGroupPermissionsService groupPermissionsService)
    : IRequestHandler<GetGroupQuery, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .Include(g => g.Name)
            .Include(g => g.Memberships)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u.Nickname)
            .Include(g => g.Memberships)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u.Rating)
            .Include(g => g.Memberships)
            .SingleOrDefaultAsync(g => g.Id == query.GroupId, cancellationToken);
        if (group is null)
        {
            return new GroupNotFoundError();
        }

        var membership = group.Memberships.SingleOrDefault(m => m.User.Id == query.UserId);
        if (membership is null)
        {
            return new ForbiddenError();
        }

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name.Value,
            Memberships = group.Memberships
                .Select(m => new GroupMembershipDto
                {
                    User = new UserDto
                    {
                        Id = m.User.Id,
                        Nickname = m.User.Nickname.Value,
                        Rating = m.User.Rating.Value,
                        CreatedAt = m.User.CreatedAt
                    },
                    Role = m.Role,
                    IsConfirmed = m.IsConfirmed
                })
                .ToList()
        };
    }
}
