using Duely.Application.UseCases.Features.CodeRuns;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.BackgroundJobs;

public sealed class ExeshSubmissionStatusRestPoller(
    IServiceScopeFactory scopeFactory,
    IOptions<ExeshStatusPollingOptions> pollingOptions,
    ILogger<ExeshSubmissionStatusRestPoller> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ExeshStatusPollingOptions _options = pollingOptions.Value;
    private readonly ILogger<ExeshSubmissionStatusRestPoller> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var count = _options.Count > 0 ? _options.Count : 20;
        var pollIntervalMs = _options.PollIntervalMs > 0 ? _options.PollIntervalMs : 1000;

        _logger.LogInformation(
            "Exesh REST status poller started. Count={Count}, PollIntervalMs={PollIntervalMs}",
            count,
            pollIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(count, stoppingToken);
                await Task.Delay(pollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exesh REST status poller failed");
                await Task.Delay(pollIntervalMs, stoppingToken);
            }
        }
    }

    private async Task PollOnceAsync(int count, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var client = scope.ServiceProvider.GetRequiredService<IExeshClient>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var pendingRuns = await context.CodeRuns
            .AsNoTracking()
            .Where(r => r.ExecutionId != null && r.Status != UserCodeRunStatus.Done)
            .Select(r => new { ExecutionId = r.ExecutionId!, r.HandledStatusCount })
            .ToListAsync(cancellationToken);

        foreach (var codeRun in pendingRuns)
        {
            var startId = codeRun.HandledStatusCount + 1;
            var eventsResult = await client.GetExecutionEventsAsync(
                codeRun.ExecutionId,
                startId,
                count,
                cancellationToken);

            if (eventsResult.IsFailed)
            {
                _logger.LogWarning(
                    "Exesh REST messages fetch failed. ExecutionId={ExecutionId}, StartId={StartId}",
                    codeRun.ExecutionId,
                    startId);
                continue;
            }

            foreach (var executionEvent in eventsResult.Value.OrderBy(e => e.EventId))
            {
                var updateResult = await mediator.Send(new UpdateCodeRunCommand
                {
                    ExecutionId = codeRun.ExecutionId,
                    Type = executionEvent.Event.Type,
                    Status = executionEvent.Event.Status,
                    Output = executionEvent.Event.Output,
                    Error = executionEvent.Event.Error ?? executionEvent.Event.CompilationError
                }, cancellationToken);

                if (updateResult.IsFailed)
                {
                    _logger.LogWarning(
                        "Exesh status update failed. ExecutionId={ExecutionId}, StartId={StartId}",
                        codeRun.ExecutionId,
                        startId);
                    break;
                }
            }
        }
    }
}
