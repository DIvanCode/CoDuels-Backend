using System.ComponentModel;
using Duely.Domain.Kernel.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public abstract class Duel : Entity
{
    protected Duel(
        DuelType type,
        DuelConfiguration configuration,
        DateTime createdAt)
    {
        Type = type;
        Configuration = configuration;
        
        Status = DuelStatus.Pending;
        CreatedAt = createdAt;
    }
    
    public int Id { get; init; }
    public DuelType Type { get; init; }
    public DuelConfiguration Configuration { get; init; }

    private readonly List<DuelProblem> _problems = [];
    public IReadOnlyCollection<DuelProblem> Problems => _problems;
    
    public DuelStatus Status { get; private set; }
    public DateTime CreatedAt { get; init; }
    // public DateTime? UpdatedAt { get; protected set; }
    // public DateTime? StartedAt { get; private set; }
    // public DateTime? FinishedAt { get; private set; }
    // public User? Winner { get; private set; }

    // public IReadOnlyCollection<Solution> Solutions { get; init; } = [];
    // public IReadOnlyCollection<Submission> Submissions { get; init; } = [];

    public void AddProblem(Problem problem, bool isVisible)
    {
        var position = _problems.Count + 1;
        _problems.Add(new DuelProblem(this, problem, position, isVisible));
    }

    // public virtual void Start(DateTime startedAt, ProblemSet problemSet)
    // {
    //     if (Status != DuelStatus.Pending)
    //     {
    //         throw new InvalidOperationException("Нельзя начать дуэль, которая уже начата.");
    //     }
    //
    //     ProblemSet = problemSet;
    //     Status = DuelStatus.InProgress;
    //     UpdatedAt = startedAt;
    //     StartedAt = startedAt;
    //     
    //     AddDomainEvent(new DuelStartedDomainEvent(Id));
    // }

    // public virtual void Finish(DateTime finishedAt, User? winner)
    // {
    //     if (Status != DuelStatus.InProgress)
    //     {
    //         throw new InvalidOperationException("Нельзя закончить дуэль, которая не в процессе.");
    //     }
    //     
    //     Status = DuelStatus.Finished;
    //     UpdatedAt = finishedAt;
    //     FinishedAt = finishedAt;
    //     Winner = winner;
    //     
    //     AddDomainEvent(new DuelFinishedDomainEvent(Id));
    // }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected Duel()
    {
    }
#pragma warning restore CS8618, CS9264
}

public enum DuelType
{
    RankedDuel = 0,
    // FriendlyDuel = 1,
    // GroupDuel = 2
    // TournamentDuel = 3
}

public enum DuelStatus
{
    Pending = 0,
    InProgress = 1,
    Finished = 2
}
