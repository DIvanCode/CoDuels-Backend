using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class AddUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class AddUserHandler(Context context, IDuelManager duelManager, ILogger<AddUserHandler> logger)
    : IRequestHandler<AddUserCommand, Result>
{
    public async Task<Result> Handle(AddUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (user is null)
        {
            logger.LogWarning("AddUserHandler failed: User {UserId} not found", command.UserId);

            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (duelManager.IsUserWaiting(user.Id))
        {
            logger.LogDebug("AddUser skipped: user {UserId} already waiting in duel queue", user.Id);

            return new EntityAlreadyExistsError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow);

        logger.LogInformation("User {UserId} added to the waiting pool with rating = {Rating}", user.Id, user.Rating);
        
        return Result.Ok();
    }
}
