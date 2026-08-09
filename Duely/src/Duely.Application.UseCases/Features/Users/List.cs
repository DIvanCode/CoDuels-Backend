using Duely.Application.UseCases.Dtos;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class GetUsersQuery : IRequest<Result<List<UserDto>>>
{
    public IReadOnlyCollection<int>? UserIds { get; init; }
}

public sealed class GetUsersHandler(Context context)
    : IRequestHandler<GetUsersQuery, Result<List<UserDto>>>
{
    public async Task<Result<List<UserDto>>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        if (query.UserIds is { Count: 0 })
        {
            return new List<UserDto>();
        }

        var users = context.Users.AsNoTracking();
        if (query.UserIds is not null)
        {
            users = users.Where(user => query.UserIds.Contains(user.Id));
        }

        return await users
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new UserDto
            {
                Id = user.Id,
                Nickname = user.Nickname,
                Rating = user.Rating,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
