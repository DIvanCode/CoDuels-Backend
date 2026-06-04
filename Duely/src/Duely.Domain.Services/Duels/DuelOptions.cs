using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public required bool DefaultShouldShowOpponentSolution { get; init; } = true;
    public required int DefaultDurationMinutes { get; init; } = 30;
    public required int DefaultProblemsCount { get; init; } = 1;
    public required ProblemsOrder DefaultProblemsOrder { get; init; } = ProblemsOrder.Sequential;
}
