using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels.RankedSearches;

public sealed class StartRankedSearchCommand : IRequest<Result>
{
    public required Guid UserId { get; init; }
}

internal sealed class StartRankedSearchHandler(
    Context context,
    ILogger<StartRankedSearchHandler> logger)
    : IRequestHandler<StartRankedSearchCommand, Result>
{
    public async Task<Result> Handle(StartRankedSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var rankedSearchExists = await context.RankedSearches
            .AsNoTracking()
            .AnyAsync(s => s.User.Id == command.UserId, cancellationToken);
        if (rankedSearchExists)
        {
            return new EntityAlreadyExistsError("Вы уже находитесь в поиске рейтинговой дуэли.");
        }

        var rankedSearchId = new RankedSearchId(Guid.NewGuid());
        var rankedSearch = new RankedSearch(rankedSearchId, user, DateTime.UtcNow);
        
        context.RankedSearches.Add(rankedSearch);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "User {Nickname} started ranked search with rating {Rating}",
            user.Nickname, user.Rating);
        
        return Result.Ok();
    }
}
