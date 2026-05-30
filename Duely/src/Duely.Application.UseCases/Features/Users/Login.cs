using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Users.Entities;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class LoginCommand : IRequest<Result<UserDto>>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
    public required string RefreshToken { get; init; }
}

internal sealed class LoginHandler(
    Context context,
    ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var nickname = new Nickname(command.Nickname);
        
        var user = await context.Users
            .Include(u => u.Nickname)
            .Include(u => u.Password)
            .SingleOrDefaultAsync(u => u.Nickname.LowerValue == nickname.LowerValue, cancellationToken);
        if (user is null || !user.Password.Verify(command.Password))
        {
            return new AuthenticationError("Неверный никнейм или пароль.");
        }
        
        var userWithRefreshTokenExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.RefreshToken == command.RefreshToken, cancellationToken);
        if (userWithRefreshTokenExists)
        {
            return new RefreshTokenAlreadyExistsError();
        }

        user.UpdateRefreshToken(command.RefreshToken);

        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} logged in", user.Nickname);

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname.Value,
            Rating = user.Rating.Value,
            CreatedAt = user.CreatedAt
        };
    }
}
