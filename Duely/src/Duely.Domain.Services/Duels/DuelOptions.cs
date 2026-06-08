using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public bool DefaultShouldShowOpponentSolution { get; init; } = true;
    public int DefaultDurationMinutes { get; init; } = 30;
    public int DefaultProblemsCount { get; init; } = 1;
    public ProblemsOrder DefaultProblemsOrder { get; init; } = ProblemsOrder.Sequential;
}
