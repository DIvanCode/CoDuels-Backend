using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.UseCases.SearchRankedDuels;

public sealed class StartRankedDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

internal sealed class StartRankedDuelSearchHandler(Context context, ILogger<StartRankedDuelSearchHandler> logger)
    : IRequestHandler<StartRankedDuelSearchCommand, Result>
{
    public async Task<Result> Handle(StartRankedDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var rankedDuelSearcherExists = await context.RankedDuelSearchers
            .Where(s => s.User.Id == command.UserId)
            .AnyAsync(cancellationToken);
        if (rankedDuelSearcherExists)
        {
            return new EntityAlreadyExistsError("Вы уже находитесь в поиске рейтинговой дуэли.");
        }

        var rankedDuelSearcher = RankedDuelSearcher.Create(user, DateTime.UtcNow);
        
        context.RankedDuelSearchers.Add(rankedDuelSearcher);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} started ranked duel search", user.Nickname);
        
        return Result.Ok();
    }
}
