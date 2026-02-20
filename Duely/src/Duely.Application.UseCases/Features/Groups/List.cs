using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Groups;

public sealed class GetUserGroupsQuery : IRequest<Result<List<GroupDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetUserGroupsHandler(Context context)
    : IRequestHandler<GetUserGroupsQuery, Result<List<GroupDto>>>
{
    public async Task<Result<List<GroupDto>>> Handle(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Include(u => u.Groups)
                .ThenInclude(g => g.Group)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }

        var result = user.Groups
            .Select(g => new GroupDto
            {
                Id = g.Group.Id,
                Name = g.Group.Name
            })
            .ToList();

        return Result.Ok(result);
    }
}
