using Duely.Application.Handlers.Users.Models;
using Duely.Application.Handlers.Users.Validators;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Handlers.Users.UseCases;

public sealed class GetUserQuery : IRequest<Result<UserDto>>
{
    public required int? Id { get; init; }
    public required string? Nickname { get; init; }
}

internal sealed class GetUserHandler(Context context) : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var userQuery = context.Users.AsNoTracking();
        
        if (query.Id is not null)
        {
            userQuery = userQuery.Where(x => x.Id == query.Id);
        }

        if (query.Nickname is not null)
        {
#pragma warning disable CA1862
            userQuery = userQuery.Where(x => x.Nickname.ToLower() == query.Nickname.ToLower());
#pragma warning restore CA1862
        }
        
        var user = await userQuery.FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new UserNotFoundError();
        }

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            CreatedAt = user.CreatedAt,
            Rating = user.Rating
        };
    }
}

internal sealed class GetUserQueryValidator : AbstractValidator<GetUserQuery>
{
    public GetUserQueryValidator(NicknameValidator nicknameValidator)
    {
        When(x => x.Nickname is not null, () =>
        {
            RuleFor(x => x.Nickname).SetValidator(nicknameValidator!);
        });
    }
}
