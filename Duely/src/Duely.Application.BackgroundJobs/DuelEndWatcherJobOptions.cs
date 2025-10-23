namespace Duely.Application.BackgroundJobs;

public sealed class DuelEndWatcherJobOptions
{
    public const string SectionName = "DuelEndWatcherJob";

    public int CheckIntervalMs { get; init; } = 3000;
}
