using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.BackgroundJobs;

public sealed class TaskiSubmissionStatusRestPoller(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskiStatusPollingOptions> pollingOptions,
    ILogger<TaskiSubmissionStatusRestPoller> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TaskiStatusPollingOptions _options = pollingOptions.Value;
    private readonly ILogger<TaskiSubmissionStatusRestPoller> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var count = _options.Count > 0 ? _options.Count : 20;
        var pollIntervalMs = _options.PollIntervalMs > 0 ? _options.PollIntervalMs : 1000;

        _logger.LogInformation(
            "Taski REST status poller started. Count={Count}, PollIntervalMs={PollIntervalMs}",
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
                _logger.LogError(e, "Taski REST status poller failed");
                await Task.Delay(pollIntervalMs, stoppingToken);
            }
        }
    }

    private async Task PollOnceAsync(int count, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var client = scope.ServiceProvider.GetRequiredService<ITaskiClient>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var pendingSubmissions = await context.Submissions
            .AsNoTracking()
            .Where(s => s.Status != SubmissionStatus.Done)
            .Select(s => new { s.Id, s.HandledStatusCount })
            .ToListAsync(cancellationToken);

        foreach (var submission in pendingSubmissions)
        {
            var startId = submission.HandledStatusCount + 1;
            var eventsResult = await client.GetSolutionEventsAsync(
                submission.Id.ToString(),
                startId,
                count,
                cancellationToken);

            if (eventsResult.IsFailed)
            {
                _logger.LogWarning(
                    "Taski REST messages fetch failed. SubmissionId={SubmissionId}, StartId={StartId}",
                    submission.Id,
                    startId);
                continue;
            }

            foreach (var solutionEvent in eventsResult.Value.OrderBy(e => e.EventId))
            {
                var updateResult = await mediator.Send(new UpdateSubmissionStatusCommand
                {
                    SubmissionId = submission.Id,
                    Type = solutionEvent.Event.Type,
                    Verdict = solutionEvent.Event.Verdict,
                    Message = solutionEvent.Event.Message ?? solutionEvent.Event.Status,
                    Error = solutionEvent.Event.Error
                }, cancellationToken);

                if (updateResult.IsFailed)
                {
                    _logger.LogWarning(
                        "Taski status update failed. SubmissionId={SubmissionId}, StartId={StartId}",
                        submission.Id,
                        startId);
                    break;
                }
            }
        }
    }
}
