using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RegisterCommand : IRequest<Result>
{
    public required string Nickname { get; init; }
    public required string Password { get; init; }
}

public sealed class RegisterHandler(Context context) : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Nickname == command.Nickname, cancellationToken);
        if (user is not null)
        {
            return new EntityAlreadyExistsError(nameof(User), nameof(User.Nickname), command.Nickname);
        }

        var passwordSalt = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password + passwordSalt.ToString(), 12);

        user = new User
        {
            Nickname = command.Nickname,
            PasswordHash = passwordHash.ToString(),
            PasswordSalt = passwordSalt.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
