namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJobOptions
{
    public const string SectionName = "DuelMakingJob";

    public int CheckPairIntervalMs { get; init; } = 3000;
}
