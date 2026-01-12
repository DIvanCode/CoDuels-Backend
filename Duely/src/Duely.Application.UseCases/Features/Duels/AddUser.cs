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
    public string? OpponentNickname { get; init; }
    public int? ConfigurationId { get; init; }
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

        int? expectedOpponentId = null;
        int? configurationId = null;
        if (!string.IsNullOrWhiteSpace(command.OpponentNickname))
        {
            var opponent = await context.Users.SingleOrDefaultAsync(
                u => u.Nickname == command.OpponentNickname,
                cancellationToken);
            if (opponent is null)
            {
                logger.LogWarning(
                    "AddUserHandler failed: Opponent {Nickname} not found for user {UserId}",
                    command.OpponentNickname,
                    user.Id);
                return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
            }

            if (opponent.Id == user.Id)
            {
                return new ForbiddenError(nameof(User), "invite", nameof(User.Id), user.Id);
            }

            expectedOpponentId = opponent.Id;

            var opponentIsWaiting = duelManager
                .GetWaitingUsers()
                .Any(waiting => waiting.UserId == opponent.Id && waiting.ExpectedOpponentId == user.Id);
            if (opponentIsWaiting && command.ConfigurationId is not null)
            {
                return new ForbiddenError(nameof(DuelConfiguration), "set", nameof(User.Id), user.Id);
            }
        }

        if (command.ConfigurationId is not null)
        {
            var configurationExists = await context.DuelConfigurations.AnyAsync(
                c => c.Id == command.ConfigurationId,
                cancellationToken);
            if (!configurationExists)
            {
                return new EntityNotFoundError(
                    nameof(DuelConfiguration),
                    nameof(DuelConfiguration.Id),
                    command.ConfigurationId.Value);
            }

            configurationId = command.ConfigurationId;
        }

        if (duelManager.IsUserWaiting(user.Id))
        {
            logger.LogDebug("AddUser skipped: user {UserId} already waiting in duel queue", user.Id);

            return new EntityAlreadyExistsError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow, expectedOpponentId, configurationId);

        logger.LogInformation("User {UserId} added to the waiting pool with rating = {Rating}", user.Id, user.Rating);
        
        return Result.Ok();
    }
}
