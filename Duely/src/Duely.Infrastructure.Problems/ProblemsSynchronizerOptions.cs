namespace Duely.Infrastructure.Problems;

internal sealed class ProblemsSynchronizerOptions
{
    public const string SectionName = "Problems:Synchronizer";
    
    public int IntervalMs { get; init; } = 5 * 60 * 1000; // 5m
}
