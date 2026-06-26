using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duels";

    public required DefaultDuelConfigurationOptions DefaultConfiguration { get; init; }
    public required RankedDuelOptions Ranked { get; init; }
}

public sealed class DefaultDuelConfigurationOptions
{
    public bool ShouldShowOpponentSolution { get; init; } = true;
    public int DurationMinutes { get; init; } = 30;
    public int ProblemsCount { get; init; } = 1;
    public ProblemsOrder ProblemsOrder { get; init; } = ProblemsOrder.Sequential;
}

public sealed class RankedDuelOptions
{
    public const string SectionName = DuelOptions.SectionName + ":Ranked";
    
    public int ConfirmationTimeoutSeconds { get; init; } = 120; // 2m
}
