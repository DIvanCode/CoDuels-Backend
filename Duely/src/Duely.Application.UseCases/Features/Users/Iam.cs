using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class IamQuery : IRequest<Result<UserDto>>
{
    public required int UserId { get; init; }
}

public sealed class IamHandler(Context context) : IRequestHandler<IamQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(IamQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            Rating = user.Rating
        };
    }
}
