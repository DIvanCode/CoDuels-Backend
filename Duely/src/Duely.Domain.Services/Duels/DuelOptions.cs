namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public int MaxDurationMinutes { get; init; } = 30;
}
