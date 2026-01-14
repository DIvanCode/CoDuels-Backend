namespace Duely.Application.Services.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int SubmissionsPerMinute { get; init; } = 5;
    public int RunsPerMinute { get; init; } = 10;
}