using Duely.Domain.Kernel.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class DuelConfiguration : Entity
{
    public DuelConfiguration(
        Guid id,
        bool isRated,
        bool shouldShowOpponentSolution,
        int durationMinutes,
        int problemsCount,
        ProblemsOrder problemsOrder,
        User? createdBy = null)
    {
        Id = id;
        IsRated = isRated;
        ShouldShowOpponentSolution = shouldShowOpponentSolution;
        DurationMinutes = durationMinutes;
        ProblemsCount = problemsCount;
        ProblemsOrder = problemsOrder;
        CreatedBy = createdBy;
    }
    
    public Guid Id { get; init; }
    
    public bool IsRated { get; init; }
    public bool ShouldShowOpponentSolution { get; private set; }
    public int DurationMinutes { get; private set; }
    public int ProblemsCount { get; private set; }
    public ProblemsOrder ProblemsOrder { get; private set; }
    
    public User? CreatedBy { get; init; }

    public void Update(
        bool shouldShowOpponentSolution,
        int durationMinutes,
        int problemsCount,
        ProblemsOrder problemsOrder)
    {
        ShouldShowOpponentSolution = shouldShowOpponentSolution;
        DurationMinutes = durationMinutes;
        ProblemsCount = problemsCount;
        ProblemsOrder = problemsOrder;
    }
}

public enum ProblemsOrder
{
    Sequential = 0,
    Parallel = 1
}
