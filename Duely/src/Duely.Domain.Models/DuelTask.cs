namespace Duely.Domain.Models;

public sealed class DuelTask(string id, int level, string[] topics)
{
    public string Id { get; } = id;
    public int Level { get; } = level;
    public string[] Topics { get; } = topics;
}
