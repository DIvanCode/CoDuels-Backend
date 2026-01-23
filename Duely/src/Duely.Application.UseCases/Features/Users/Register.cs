using System.Text.RegularExpressions;
using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RegisterCommand : IRequest<Result>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
}

public sealed class RegisterHandler(Context context, ILogger<RegisterHandler> logger)
    : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Nickname == command.Nickname, cancellationToken);
        if (user is not null)
        {
            return new EntityAlreadyExistsError(nameof(User), nameof(User.Nickname), command.Nickname);
        }

        var passwordSalt = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password + passwordSalt, 12);

        user = new User
        {
            Nickname = command.Nickname,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt.ToString(),
            CreatedAt = DateTime.UtcNow,
            Rating = 1500
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User registered. UserId = {UserId}, Nickname = {Nickname}", user.Id, user.Nickname);

        return Result.Ok();
    }
}

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex NicknameRegex = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled); 
    
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Nickname).Matches(NicknameRegex).WithMessage("Invalid nickname.");
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}
