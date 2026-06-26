using Duely.Application.Handlers.Users.Models;
using Duely.Application.Handlers.Users.Validators;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Users.UseCases;

public sealed class LoginCommand : IRequest<Result<UserDto>>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
    public required string RefreshToken { get; init; }
}

internal sealed class LoginHandler(Context context, ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
#pragma warning disable CA1862
            .Where(u => u.Nickname.ToLower() == command.Nickname.ToLower())
#pragma warning restore CA1862
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(command.Password + user.PasswordSalt, user.PasswordHash))
        {
            return new AuthenticationError("Неверный никнейм или пароль.");
        }
        
        var userWithRefreshTokenExists = await context.Users
            .Where(u => u.RefreshToken == command.RefreshToken)
            .AnyAsync(cancellationToken);
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
            Nickname = user.Nickname,
            CreatedAt = user.CreatedAt,
            Rating = user.Rating
        };
    }
}

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator(
        NicknameValidator nicknameValidator,
        PasswordValidator passwordValidator,
        RefreshTokenValidator refreshTokenValidator)
    {
        RuleFor(x => x.Nickname).SetValidator(nicknameValidator);
        
        RuleFor(x => x.Password).SetValidator(passwordValidator);
        
        RuleFor(x => x.RefreshToken).SetValidator(refreshTokenValidator);
    }
}
