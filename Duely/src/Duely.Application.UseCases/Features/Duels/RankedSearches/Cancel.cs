using Duely.Domain.Common.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.RankedSearches;

public sealed class CancelRankedSearchCommand : IRequest<Result>
{
    public required Guid UserId { get; init; }
}

internal sealed class CancelRankedSearchHandler(
    Context context,
    ILogger<CancelRankedSearchHandler> logger)
    : IRequestHandler<CancelRankedSearchCommand, Result>
{
    public async Task<Result> Handle(CancelRankedSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var rankedSearch = await context.RankedSearches
            .SingleOrDefaultAsync(s => s.User.Id == command.UserId, cancellationToken);
        if (rankedSearch is null)
        {
            return new EntityNotFoundError("Вы не находитесь в поиске рейтинговой дуэли.");
        }

        rankedSearch.Cancel();
        
        context.RankedSearches.Remove(rankedSearch);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} canceled ranked search", user.Nickname);
        
        return Result.Ok();
    }
}
