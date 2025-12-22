using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class RemoveUserCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class RemoveUserHandler(Context context, IDuelManager duelManager, ILogger<RemoveUserHandler> logger)
    : IRequestHandler<RemoveUserCommand, Result>
{
    public async Task<Result> Handle(RemoveUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == command.UserId,
            cancellationToken);
        if (user is null)
        {
            logger.LogWarning("AddUserHandler failed: User {UserId} not found", command.UserId);
            
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (!duelManager.IsUserWaiting(user.Id))
        {
            logger.LogDebug("AddUser skipped: user {UserId} already waiting in duel queue", user.Id);

            return new EntityNotFoundError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.RemoveUser(user.Id);
        
        logger.LogInformation("User {UserId} removed from the waiting pool", user.Id);
        
        return Result.Ok();
    }
}
