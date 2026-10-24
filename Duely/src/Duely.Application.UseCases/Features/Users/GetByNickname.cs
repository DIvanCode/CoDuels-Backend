using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class GetUserByNicknameQuery : IRequest<Result<UserDto>>
{
    public required string Nickname { get; init; }
}

public sealed class GetByNicknameHandler(Context context) : IRequestHandler<GetUserByNicknameQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByNicknameQuery query, CancellationToken cancellationToken)
    {
        var nicknameLower = User.NormalizeNickname(query.Nickname);
        var user = await context.Users.SingleOrDefaultAsync(u => u.NicknameLower == nicknameLower, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), query.Nickname);
        }

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            Rating = user.Rating,
            CreatedAt = user.CreatedAt,
            LastSeenAt = user.LastSeenAt
        };
    }
}
