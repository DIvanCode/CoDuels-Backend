namespace Duely.Domain.Models.Duels;

public sealed class DuelTask(string id, int level, string[] topics)
{
    public string Id { get; } = id;
    public int Level { get; } = level;
    public string[] Topics { get; } = topics;
}
