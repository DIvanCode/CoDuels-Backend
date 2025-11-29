namespace Duely.Domain.Models;

public sealed class DuelTask(string id, int level)
{
    public string Id { get; } = id;
    public int Level { get; } = level;
}
