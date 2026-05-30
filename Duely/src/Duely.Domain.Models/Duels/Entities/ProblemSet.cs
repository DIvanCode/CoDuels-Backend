using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class ProblemSet : ValueObject
{
    public ProblemSet(ICollection<Problem> problems)
    {
        var distinctPositions = problems.Select(p => p.Position).Distinct().ToList();
        
        if (distinctPositions.Count != problems.Count)
        {
            throw new ArgumentException("Набор задач не может содержать задачи с одинаковой позицией.");
        }

        if (distinctPositions.Min() != 1)
        {
            throw new ArgumentException("Набор задач должен начинаться задачей с позицией 1.");
        }
        
        if (distinctPositions.Max() != problems.Count)
        {
            throw new ArgumentException("Набор задач должен заканчиваться задачей с позицией, равной количеству задач");
        }
        
        Problems = problems.ToList();
    }

    public IReadOnlyCollection<Problem> Problems { get; init; }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return Problems
            .OrderBy(p => p.Position)
            .Select(problem => problem.ExternalId);
    }
}

public sealed class Problem : ValueObject
{
    public Problem(int position, bool isVisible, string externalId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        
        Position = position;
        IsVisible = isVisible;
        ExternalId = externalId;
    }
    
    public int Position { get; init; }
    public bool IsVisible { get; private set; }
    public string ExternalId { get; init; }

    public void ChangeVisibility(bool isVisible)
    {
        IsVisible = isVisible;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExternalId;
    }
}
