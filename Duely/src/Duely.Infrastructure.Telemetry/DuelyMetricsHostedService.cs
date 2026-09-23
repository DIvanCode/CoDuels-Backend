using Microsoft.Extensions.Hosting;

namespace Duely.Infrastructure.Telemetry;

public sealed class DuelyMetricsHostedService(DuelyMetrics metrics) : IHostedService
{
    // Constructing DuelyMetrics registers the observable instruments.
    private readonly DuelyMetrics _metrics = metrics;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
