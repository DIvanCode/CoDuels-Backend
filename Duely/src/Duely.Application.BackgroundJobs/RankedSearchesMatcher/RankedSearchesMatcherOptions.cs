namespace Duely.Application.BackgroundJobs.RankedSearchesMatcher;

internal sealed class RankedSearchesMatcherOptions
{
    public const string SectionName = "RankedSearchesMatcher";

     public int IntervalMs { get; init; } = 1000;
}
