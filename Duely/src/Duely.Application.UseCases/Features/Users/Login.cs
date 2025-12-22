using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Users;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class LoginCommand : IRequest<Result<TokenDto>>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginHandler(Context context, ITokenService tokenService, ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Nickname == command.Nickname, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("LoginHandler failed: user with nickname {Nickname} is not found", command.Nickname);

            return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.Nickname);
        }

        if (!BCrypt.Net.BCrypt.Verify(command.Password + user.PasswordSalt, user.PasswordHash))
        {
            logger.LogWarning("LoginHandler failed: invalid passward. UserId = {UserId}", user.Id);

            return new AuthenticationError();
        }

        logger.LogInformation("Login success. UserId = {UserId}", user.Id);

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user);
        user.RefreshToken = refreshToken;

        await context.SaveChangesAsync(cancellationToken);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
