namespace Duely.Application.BackgroundJobs;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int CheckIntervalMs { get; init; } = 1000;
    public int BatchSize { get; init; } = 1;
    public int InitialRetryDelayMs { get; init; } = 20000;
    public int MaxRetryDelayMs { get; init; } = 30000;
}
