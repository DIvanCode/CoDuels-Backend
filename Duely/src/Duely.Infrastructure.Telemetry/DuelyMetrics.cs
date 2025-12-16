using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Telemetry;

public sealed class DuelyMetrics
{
    public DuelyMetrics(MetricsSnapshot snapshot, IOptions<OtelOptions> otel)
    {
        var meter = new Meter(otel.Value.MeterName);

        meter.CreateObservableGauge<long>(
            "waiting_users",
            () => snapshot.WaitingUsers
        );

        CreateByStatusGauge(meter, "duels", snapshot.GetDuels);
        CreateByStatusGauge(meter, "submissions", snapshot.GetSubmissions);
        CreateByStatusGauge(meter, "runs", snapshot.GetRuns);
        CreateByStatusGauge(meter, "outbox", snapshot.GetOutbox);
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