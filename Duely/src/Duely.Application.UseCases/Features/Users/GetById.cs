using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class GetUserByIdQuery : IRequest<Result<UserDto>>
{
    public required Guid Id { get; init; }
}

internal sealed class GetUserByIdHandler(Context context) : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .SingleOrDefaultAsync(u => u.Id == query.Id, cancellationToken);
        if (user is null)
        {
            return new UserNotFoundError();
        }

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname.Value,
            Rating = user.Rating.Value,
            CreatedAt = user.CreatedAt
        };
    }
}
