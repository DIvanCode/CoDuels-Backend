namespace Duely.Infrastructure.Problems.Taski;

internal sealed class TaskiOptions
{
    public const string SectionName = "Problems:Gateway:Taski";

    public required bool IsEnabled { get; init; }
    public required string BaseUrl { get; init; }
}
