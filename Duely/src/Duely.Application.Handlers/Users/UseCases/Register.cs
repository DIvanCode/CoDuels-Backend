using Duely.Application.Handlers.Users.Validators;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Users.Entities;
using Duely.Domain.Services.Users;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.Handlers.Users.UseCases;

public sealed class RegisterCommand : IRequest<Result>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
}

internal sealed class RegisterHandler(
    Context context,
    IOptions<UserOptions> userOptions,
    ILogger<RegisterHandler> logger)
    : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var userExists = await context.Users
#pragma warning disable CA1862
            .Where(u => u.Nickname.ToLower() == command.Nickname.ToLower())
#pragma warning restore CA1862
            .AnyAsync(cancellationToken);
        if (userExists)
        {
            return new ConflictError("Пользователь с заданным никнеймом уже существует.");
        }

        var passwordSalt = Guid.NewGuid().ToString();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password + passwordSalt, 12);
        var rating = userOptions.Value.InitialRating;
        var user = new User(command.Nickname, passwordHash, passwordSalt, rating);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Nickname} registered", user.Nickname);

        return Result.Ok();
    }
}

internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(NicknameValidator nicknameValidator, PasswordValidator passwordValidator)
    {
        RuleFor(x => x.Nickname).SetValidator(nicknameValidator);
        
        RuleFor(x => x.Password).SetValidator(passwordValidator);
    }
}
