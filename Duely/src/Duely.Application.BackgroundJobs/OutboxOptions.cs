namespace Duely.Application.BackgroundJobs;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int CheckIntervalMs { get; init; } = 3000;    
    public int RetryDelayMs { get; init; } = 20000;
}
