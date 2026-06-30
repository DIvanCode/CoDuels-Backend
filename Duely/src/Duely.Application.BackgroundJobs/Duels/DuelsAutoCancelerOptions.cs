using Duely.Domain.Services.Duels;

namespace Duely.Application.BackgroundJobs.Duels;

internal sealed class DuelsAutoCancelerOptions
{
    public const string SectionName = RankedDuelOptions.SectionName + ":AutoCanceler";

    public int IntervalMs { get; init; } = 1000;
}
