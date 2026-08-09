using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels.Search;

public sealed class AddInactiveBotsToRankedSearchCommand : IRequest<Result>;

public sealed class AddInactiveBotsToRankedSearchHandler(Context context)
    : IRequestHandler<AddInactiveBotsToRankedSearchCommand, Result>
{
    public async Task<Result> Handle(AddInactiveBotsToRankedSearchCommand command, CancellationToken cancellationToken)
    {
        var bots = await context.Users
            .Where(u => u.IsBot)
            .ToListAsync(cancellationToken);
        
        var pendingBots = await context.PendingDuels.OfType<RankedPendingDuel>()
            .Where(p => p.User.IsBot)
            .Select(p => p.User.Id)
            .ToHashSetAsync(cancellationToken);
        var activeBots = await context.Duels
            .Where(d => d.Status != DuelStatus.Finished && (d.User1.IsBot || d.User2.IsBot))
            .Select(d => d.User1.IsBot ? d.User1.Id : d.User2.Id)
            .ToHashSetAsync(cancellationToken);
        
        var inactiveBots = bots
            .Where(b => !pendingBots.Contains(b.Id) && !activeBots.Contains(b.Id))
            .ToList();
        foreach (var bot in inactiveBots)
        {
            var pendingDuel = new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = bot,
                Rating = bot.Rating,
                CreatedAt = DateTime.UtcNow
            };
            context.PendingDuels.Add(pendingDuel);
        }
        
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
