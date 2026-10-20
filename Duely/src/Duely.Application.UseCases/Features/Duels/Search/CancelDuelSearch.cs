using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Search;

public sealed class CancelDuelSearchCommand : IRequest<Result>
{
    public required int UserId { get; init; }
}

public sealed class CancelDuelSearchHandler(Context context) : IRequestHandler<CancelDuelSearchCommand, Result>
{
    public async Task<Result> Handle(CancelDuelSearchCommand command, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken))
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        var rankedPendingDuels = await context.PendingDuels.OfType<RankedPendingDuel>()
            .Where(duel => duel.User.Id == command.UserId)
            .ToListAsync(cancellationToken);
        context.PendingDuels.RemoveRange(rankedPendingDuels);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
