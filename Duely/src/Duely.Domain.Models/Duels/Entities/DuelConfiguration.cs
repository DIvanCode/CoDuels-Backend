using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class DuelConfiguration : Entity<DuelConfigurationId>
{
    public const int MaxDurationMinutes = 60;
    public const int MaxProblemsCount = 8;
    
    public DuelConfiguration(
        DuelConfigurationId id,
        bool isRated,
        bool shouldShowOpponentSolution,
        int durationMinutes,
        int problemsCount,
        ProblemsOrder problemsOrder,
        User? createdBy) : base(id)
    {
        Validate(durationMinutes, problemsCount);
        
        IsRated = isRated;
        ShouldShowOpponentSolution = shouldShowOpponentSolution;
        DurationMinutes = durationMinutes;
        ProblemsCount = problemsCount;
        ProblemsOrder = problemsOrder;
        CreatedBy = createdBy;
    }

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
        Validate(durationMinutes, problemsCount);
        
        ShouldShowOpponentSolution = shouldShowOpponentSolution;
        DurationMinutes = durationMinutes;
        ProblemsCount = problemsCount;
        ProblemsOrder = problemsOrder;
    }

    private static void Validate(int durationMinutes, int problemsCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationMinutes, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(durationMinutes, MaxDurationMinutes);
        
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(problemsCount, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(problemsCount, MaxProblemsCount);
    }
}

public sealed record DuelConfigurationId(Guid Value) : Identity<Guid>(Value);

public enum ProblemsOrder
{
    Sequential = 0,
    Parallel = 1
}
