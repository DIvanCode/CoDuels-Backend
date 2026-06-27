namespace Duely.Domain.Models.Duels.Entities;

public sealed class Problem
{
    public Problem(string externalSystemName, string externalId, string title)
    {
        ExternalSystemName = externalSystemName;
        ExternalId = externalId;
        Title = title;
    }

    public int Id { get; init; }
    public string ExternalSystemName { get; init; }
    public string ExternalId { get; init; }
    public string Title { get; private set; }

    public void Update(string title)
    {
        Title = title;
    }
}
