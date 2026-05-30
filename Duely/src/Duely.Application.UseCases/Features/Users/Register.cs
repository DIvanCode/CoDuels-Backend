using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Users.Entities;
using Duely.Domain.Models.Users.Errors;
using Duely.Domain.Services.Users;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.UseCases.Features.Users;

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
        var nickname = new Nickname(command.Nickname);
        
        var userExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Nickname.LowerValue == nickname.LowerValue, cancellationToken);
        if (userExists)
        {
            return new EntityAlreadyExistsError("Пользователь с заданным никнеймом уже существует.");
        }

        var id = new UserId(Guid.NewGuid());
        var password = new Password(command.Password);
        var rating = new Rating(userOptions.Value.InitialRating);

        var user = new User(id, nickname, password, DateTime.UtcNow, rating);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Nickname} registered", user.Nickname);

        return Result.Ok();
    }
}

internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("Никнейм не может быть пустым.")
            .MaximumLength(Nickname.MaxLength).WithMessage($"Никнейм не может содержать более {Nickname.MaxLength} символов.")
            .Matches(Nickname.Regex).WithMessage("Никнейм содержит недопустимые символы.");
        RuleFor(x => x.Password)
            .MinimumLength(Password.MinLength).WithMessage($"Пароль должен содержать не менее {Password.MinLength} символов.")
            .MaximumLength(Password.MaxLength).WithMessage($"Пароль не может содержать более {Password.MaxLength} символов.");
    }
}
