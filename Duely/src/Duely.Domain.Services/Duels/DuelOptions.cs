using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public required bool DefaultShouldShowOpponentSolution { get; init; } = true;
    public required int DefaultMaxDurationMinutes { get; init; } = 30;
    public required int DefaultTasksCount { get; init; } = 1;
    public required DuelTasksOrder DefaultTasksOrder { get; init; } = DuelTasksOrder.Sequential;
}
