using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs.Duels;

internal sealed class DuelsAutoCanceler(
    IOptions<DuelsAutoCancelerOptions> options,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DuelsAutoCanceler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Duels auto canceler started with interval {Interval} ms",
            options.Value.IntervalMs);
        await Task.Yield();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WorkAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Duels auto canceler unexpected error");
            }
            
            await Task.Delay(options.Value.IntervalMs, cancellationToken);
        }
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var pendingDuels = await context.Duels
            .Where(d => d.Status == DuelStatus.Pending)
            .Include(d => d.Participants)
            .ThenInclude(p => p.User)
            .ToListAsync(cancellationToken);
        var duelsToCancel = pendingDuels
            .Where(d => d.CreatedAt.Add(d.ConfirmTimeout) < DateTime.UtcNow &&
                        d.Participants.Any(p => !p.IsReady))
            .ToList();
        
        duelsToCancel.ForEach(d => d.Cancel());
        
        await context.SaveChangesAsync(cancellationToken);
        
        duelsToCancel.ForEach(duel => logger.LogInformation("Duel {Id} canceled after confirm timeout", duel.Id));
    }
}
