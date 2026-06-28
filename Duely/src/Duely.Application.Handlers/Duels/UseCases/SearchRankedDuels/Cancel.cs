using Duely.Domain.Kernel.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.UseCases.SearchRankedDuels;

public sealed class CancelRankedDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

internal sealed class CancelRankedDuelSearchHandler(Context context, ILogger<CancelRankedDuelSearchHandler> logger)
    : IRequestHandler<CancelRankedDuelSearchCommand, Result>
{
    public async Task<Result> Handle(CancelRankedDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var rankedDuelSearcher = await context.RankedDuelSearchers
            .Where(s => s.User.Id == command.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (rankedDuelSearcher is null)
        {
            return new InvalidOperationError("Вы не находитесь в поиске рейтинговой дуэли.");
        }

        context.RankedDuelSearchers.Remove(rankedDuelSearcher);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} canceled ranked duel search", user.Nickname);
        
        return Result.Ok();
    }
}
