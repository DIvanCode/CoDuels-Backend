using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Models.Users.Entities;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class GetUserByNicknameQuery : IRequest<Result<UserDto>>
{
    public required string Nickname { get; init; }
}

internal sealed class GetUserByNicknameHandler(Context context) : IRequestHandler<GetUserByNicknameQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByNicknameQuery query, CancellationToken cancellationToken)
    {
        var nickname = new Nickname(query.Nickname);
        
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .SingleOrDefaultAsync(u => u.Nickname.LowerValue == nickname.LowerValue, cancellationToken);
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
