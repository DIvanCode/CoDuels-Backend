using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Telemetry;

public sealed class MetricsCollector : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MetricsSnapshot _snapshot;
    private readonly ILogger<MetricsCollector> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    public MetricsCollector(
        IServiceScopeFactory scopeFactory,
        MetricsSnapshot snapshot,
        ILogger<MetricsCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _snapshot = snapshot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Context>();

                var waitingUsers = await db.PendingDuels
                    .OfType<RankedPendingDuel>()
                    .LongCountAsync(stoppingToken);
                _snapshot.SetWaitingUsers(waitingUsers);

                var duels = await db.Duels
                    .AsNoTracking()
                    .GroupBy(d => d.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(d => d.Status, d => d.Count, stoppingToken);
                _snapshot.SetDuels(duels);

                var submissions = await db.Submissions
                    .AsNoTracking()
                    .GroupBy(s => s.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(s => s.Status, s => s.Count, stoppingToken);
                _snapshot.SetSubmissions(submissions);

                var codeRuns = await db.CodeRuns
                    .AsNoTracking()
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(r => r.Status, r => r.Count, stoppingToken);
                _snapshot.SetCodeRuns(codeRuns);

                var outboxMessages = await db.OutboxMessages
                    .AsNoTracking()
                    .GroupBy(o => o.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(o => o.Status, o => o.Count, stoppingToken);
                _snapshot.SetOutboxMessages(outboxMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to collect metrics");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
