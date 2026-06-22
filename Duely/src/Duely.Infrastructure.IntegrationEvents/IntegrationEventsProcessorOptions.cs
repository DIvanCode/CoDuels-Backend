namespace Duely.Infrastructure.IntegrationEvents;

internal sealed class IntegrationEventsProcessorOptions
{
    public const string SectionName = "IntegrationEvents:Processor";

    public int IntervalMs { get; init; } = 1000;
    public int BatchSize { get; init; } = 100;
    public int InitialNextProcessAttemptDelayMs { get; init; } = 50;
    public int NextProcessAttemptDelayStepMs { get; init; } = 100;
    public int MaxNextProcessAttemptDelayMs { get; init; } = 10000;
}
