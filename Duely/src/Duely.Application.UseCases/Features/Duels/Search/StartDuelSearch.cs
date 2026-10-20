using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Search;

public sealed class StartDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class StartDuelSearchHandler(Context context) : IRequestHandler<StartDuelSearchCommand, Result>
{
    public async Task<Result> Handle(StartDuelSearchCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var hasRankedPendingDuel = await context.PendingDuels.OfType<RankedPendingDuel>()
            .AsNoTracking()
            .AnyAsync(d => d.User.Id == command.UserId, cancellationToken);
        if (hasRankedPendingDuel)
        {
            return Result.Ok();
        }

        var rankedPendingDuel = new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = user,
            Rating = user.Rating,
            CreatedAt = DateTime.UtcNow
        };
        context.PendingDuels.Add(rankedPendingDuel);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
