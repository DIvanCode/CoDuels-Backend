namespace Duely.Infrastructure.Gateway.Analyzer;

public sealed class AnalyzerOptions
{
    public const string SectionName = "Analyzer";

    public required string BaseUrl { get; init; }
}
