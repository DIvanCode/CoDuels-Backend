using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class AddUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class AddUserHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<AddUserCommand, Result>
{
    public async Task<Result> Handle(AddUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (duelManager.IsUserWaiting(user.Id))
        {
            return new EntityAlreadyExistsError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow);
        Console.WriteLine($"Added user {user.Id}");
        
        return Result.Ok();
    }
}
