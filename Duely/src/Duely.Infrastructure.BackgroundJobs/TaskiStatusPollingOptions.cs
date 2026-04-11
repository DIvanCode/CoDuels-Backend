namespace Duely.Infrastructure.BackgroundJobs;

public sealed class TaskiStatusPollingOptions
{
    public const string SectionName = "Taski:StatusUpdates";

    public string Mode { get; init; } = "kafka";
    public int Count { get; init; } = 20;
    public int PollIntervalMs { get; init; } = 1000;
}
