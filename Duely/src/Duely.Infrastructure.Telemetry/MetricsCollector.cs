using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models;
using Duely.Domain.Models.Outbox;
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
                var now = DateTime.UtcNow;
                var staleBefore = now.AddMinutes(-10);

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

                var staleSubmissions = await db.Submissions
                    .LongCountAsync(s => s.Status == SubmissionStatus.Queued && s.SubmitTime <= staleBefore, stoppingToken);
                var oldestSubmission = await db.Submissions
                    .Where(s => s.Status == SubmissionStatus.Queued)
                    .Select(s => (DateTime?)s.SubmitTime)
                    .MinAsync(stoppingToken);
                _snapshot.SetQueued("submission", staleSubmissions,
                    oldestSubmission is null ? 0 : Math.Max(0, (now - oldestSubmission.Value).TotalSeconds));

                var testingFailed = await db.Submissions.LongCountAsync(s =>
                    s.Status == SubmissionStatus.Done &&
                    s.Verdict == "Testing Failed" &&
                    s.CompletedAt >= now.AddMinutes(-5), stoppingToken);
                _snapshot.SetTestingFailedLastFiveMinutes(testingFailed);

                var codeRuns = await db.CodeRuns
                    .AsNoTracking()
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(r => r.Status, r => r.Count, stoppingToken);
                _snapshot.SetCodeRuns(codeRuns);

                var staleCodeRuns = await db.CodeRuns
                    .LongCountAsync(r => r.Status == UserCodeRunStatus.Queued && r.CreatedAt <= staleBefore, stoppingToken);
                var oldestCodeRun = await db.CodeRuns
                    .Where(r => r.Status == UserCodeRunStatus.Queued)
                    .Select(r => (DateTime?)r.CreatedAt)
                    .MinAsync(stoppingToken);
                _snapshot.SetQueued("code_run", staleCodeRuns,
                    oldestCodeRun is null ? 0 : Math.Max(0, (now - oldestCodeRun.Value).TotalSeconds));

                var outboxMessages = await db.OutboxMessages
                    .AsNoTracking()
                    .GroupBy(o => o.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.LongCount() })
                    .ToDictionaryAsync(o => o.Status, o => o.Count, stoppingToken);
                _snapshot.SetOutboxMessages(outboxMessages);

                var dispatch = await db.OutboxMessages
                    .AsNoTracking()
                    .Where(o => o.Type == OutboxType.TestSolution || o.Type == OutboxType.RunUserCode)
                    .GroupBy(o => new { o.Type, o.Status })
                    .Select(g => new { g.Key.Type, g.Key.Status, Count = g.LongCount() })
                    .ToListAsync(stoppingToken);
                var dispatchCounts = new Dictionary<(string Type, string Status), long>();
                foreach (var type in new[] { OutboxType.TestSolution, OutboxType.RunUserCode })
                {
                    foreach (var status in Enum.GetValues<OutboxStatus>())
                    {
                        dispatchCounts[(type.ToString(), status.ToString())] = 0;
                    }
                }
                foreach (var item in dispatch)
                {
                    dispatchCounts[(item.Type.ToString(), item.Status.ToString())] = item.Count;
                }
                _snapshot.SetOutboxDispatch(dispatchCounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to collect metrics");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
