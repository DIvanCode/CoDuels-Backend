using Duely.Domain.Services.Duels;

namespace Duely.Application.BackgroundJobs.Duels;

internal sealed class RankedDuelSearchersMatcherOptions
{
    public const string SectionName = RankedDuelOptions.SectionName + ":SearchersMatcher";

    public int IntervalMs { get; init; } = 1000;
}
