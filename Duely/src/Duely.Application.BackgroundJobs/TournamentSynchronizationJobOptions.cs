namespace Duely.Application.BackgroundJobs;

public sealed class TournamentSynchronizationJobOptions
{
    public const string SectionName = "TournamentSynchronizationJob";
    public int CheckIntervalMs { get; init; } = 5000;
}
