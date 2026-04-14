namespace Duely.Application.BackgroundJobs;

public sealed class AnticheatBackgroundServiceOptions
{
    public const string SectionName = "AnticheatBackgroundService";

    public int CheckIntervalMs { get; init; } = 10000;
}
