using Duely.Application.Services.Errors;
using MediatR;
using FluentResults;
using Duely.Domain.Services.Duels;
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
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        int? expectedOpponentId = null;
        int? configurationId = null;
        if (!string.IsNullOrWhiteSpace(command.OpponentNickname))
        {
            var opponent = await context.Users
                .SingleOrDefaultAsync(u => u.Nickname == command.OpponentNickname, cancellationToken);
            if (opponent is null)
            {
                return new EntityNotFoundError(nameof(User), nameof(User.Nickname), command.OpponentNickname);
            }

            if (opponent.Id == user.Id)
            {
                return new ForbiddenError(nameof(User), "invitation to duel", nameof(User.Id), user.Id);
            }

            expectedOpponentId = opponent.Id;
        }

        if (command.ConfigurationId is not null)
        {
            var configurationExists = await context.DuelConfigurations
                .AnyAsync(c => c.Id == command.ConfigurationId, cancellationToken);
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
            return new EntityAlreadyExistsError(nameof(User), nameof(User.Id), user.Id);
        }
        
        duelManager.AddUser(user.Id, user.Rating, DateTime.UtcNow, expectedOpponentId, configurationId);

        logger.LogInformation("User {UserId} added to the waiting pool", user.Id);
        
        return Result.Ok();
    }
}
