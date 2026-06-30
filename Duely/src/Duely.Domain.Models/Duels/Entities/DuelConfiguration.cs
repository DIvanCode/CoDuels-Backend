using System.ComponentModel;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class DuelConfiguration
{
    public DuelConfiguration(
        bool isRated,
        bool showOpponentSolution,
        int durationMinutes,
        int problemsCount,
        ProblemsOrder problemsOrder,
        User? createdBy = null)
    {
        IsRated = isRated;
        ShowOpponentSolution = showOpponentSolution;
        DurationMinutes = durationMinutes;
        ProblemsCount = problemsCount;
        ProblemsOrder = problemsOrder;
        CreatedBy = createdBy;
    }
    
    public int Id { get; init; }
    public bool IsRated { get; init; }
    public bool ShowOpponentSolution { get; private set; }
    public int DurationMinutes { get; private set; }
    public int ProblemsCount { get; private set; }
    public ProblemsOrder ProblemsOrder { get; private set; }
    
    public User? CreatedBy { get; init; }

    // public void Update(
    //     bool shouldShowOpponentSolution,
    //     int durationMinutes,
    //     int problemsCount,
    //     ProblemsOrder problemsOrder)
    // {
    //     ShouldShowOpponentSolution = shouldShowOpponentSolution;
    //     DurationMinutes = durationMinutes;
    //     ProblemsCount = problemsCount;
    //     ProblemsOrder = problemsOrder;
    // }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private DuelConfiguration()
    {
    }
#pragma warning restore CS8618, CS9264
}

public enum ProblemsOrder
{
    Sequential = 0,
    Parallel = 1
}
