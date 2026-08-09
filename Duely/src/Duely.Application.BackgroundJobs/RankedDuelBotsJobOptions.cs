namespace Duely.Application.BackgroundJobs;

public sealed class RankedDuelBotsJobOptions
{
    public const string SectionName = "RankedDuelBots";

    public int CheckIntervalMs { get; init; } = 5000;
}
