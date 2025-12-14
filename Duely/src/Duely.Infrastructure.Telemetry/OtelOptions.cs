namespace Duely.Infrastructure.Telemetry;

public sealed class OtelOptions
{
    public const string SectionName = "Otel";

    public bool IsEnabled { get; init; }
    public string ServiceName { get; init; } = "Duely";
    public string MeterName { get; init; } = "Duely";
}
