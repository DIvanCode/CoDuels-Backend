namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public required int DefaultMaxDurationMinutes { get; init; } = 30;
}
