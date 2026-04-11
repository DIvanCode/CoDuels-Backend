namespace Duely.Infrastructure.BackgroundJobs;

public sealed class ExeshStatusPollingOptions
{
    public const string SectionName = "Exesh:StatusUpdates";

    public string Mode { get; init; } = "kafka";
    public int Count { get; init; } = 20;
    public int PollIntervalMs { get; init; } = 1000;
}
