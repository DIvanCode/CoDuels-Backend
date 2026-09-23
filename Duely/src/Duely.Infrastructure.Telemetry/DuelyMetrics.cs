using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Telemetry;

public sealed class DuelyMetrics
{
    private readonly Meter _meter;

    public DuelyMetrics(MetricsSnapshot snapshot, IOptions<OtelOptions> otel)
    {
        _meter = new Meter(otel.Value.MeterName);

        _meter.CreateObservableGauge<long>(
            "waiting_users",
            () => snapshot.WaitingUsers
        );

        CreateByStatusGauge(_meter, "duels", snapshot.GetDuels);
        CreateByStatusGauge(_meter, "submissions", snapshot.GetSubmissions);
        CreateByStatusGauge(_meter, "runs", snapshot.GetRuns);
        CreateByStatusGauge(_meter, "outbox", snapshot.GetOutbox);

        _meter.CreateObservableGauge<long>("queued_stale", () =>
            snapshot.GetQueuedStale().Select(kv =>
                new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("kind", kv.Key))));

        _meter.CreateObservableGauge<double>("queued_oldest_age_seconds", () =>
            snapshot.GetQueuedOldestAge().Select(kv =>
                new Measurement<double>(kv.Value, new KeyValuePair<string, object?>("kind", kv.Key))));

        _meter.CreateObservableGauge<long>(
            "submissions_testing_failed_last_5m",
            () => snapshot.TestingFailedLastFiveMinutes);

        _meter.CreateObservableGauge<long>("outbox_dispatch", () =>
            snapshot.GetOutboxDispatch().Select(kv => new Measurement<long>(kv.Value, new[]
            {
                new KeyValuePair<string, object?>("type", kv.Key.Type),
                new KeyValuePair<string, object?>("status", kv.Key.Status)
            })));

        _meter.CreateObservableGauge<long>("status_poller_last_success_timestamp_seconds", () =>
            snapshot.GetPollerLastSuccess().Select(kv =>
                new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("source", kv.Key))));
    }

    private static void CreateByStatusGauge(
        Meter meter,
        string name,
        Func<IReadOnlyDictionary<string, long>> getValues)
    {
        meter.CreateObservableGauge<long>(
            name,
            observeValues: () => getValues()
                .Select(kv => new Measurement<long>(kv.Value, new[] { new KeyValuePair<string, object?>("status", kv.Key) }))
                .ToArray()
        );
    }
}
