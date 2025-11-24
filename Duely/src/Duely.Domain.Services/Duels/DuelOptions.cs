namespace Duely.Domain.Services.Duels;

public sealed class DuelOptions
{
    public const string SectionName = "Duel";

    public int MaxDurationMinutes { get; init; } = 30;

    public RatingToTaskLevelMappingItem[] RatingToTaskLevelMapping { get; init; } = [];
}

public sealed class RatingToTaskLevelMappingItem
{
    public required string Rating { get; init; }
    public required int Level { get; init; }

    public (int MinRating, int MaxRating) GetInterval()
    {
        var parts = Rating.Split('-');
        var minRating = int.Parse(parts[0]);
        var maxRating = int.Parse(parts[1]);
        return (minRating, maxRating);
    }
}
