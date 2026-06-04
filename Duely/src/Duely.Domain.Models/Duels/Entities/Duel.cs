using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public abstract class Duel : Entity<DuelId>
{
    protected Duel(
        DuelId id,
        DuelType type,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt) : base(id)
    {
        var distinctParticipants = participants.Distinct().ToList();
        ArgumentOutOfRangeException.ThrowIfNotEqual(distinctParticipants.Count, 2, nameof(distinctParticipants.Count));
        
        Type = type;
        Configuration = configuration;
        Participants = participants.ToList();
        
        Status = DuelStatus.Pending;
        CreatedAt = createdAt;
    }
    
    public DuelType Type { get; init; }
    public DuelConfiguration Configuration { get; init; }
    public IReadOnlyCollection<User> Participants { get; init; }
    public ProblemSet? ProblemSet { get; private set; }
    
    public DuelStatus Status { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; protected set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public User? Winner { get; private set; }

    public IReadOnlyCollection<Solution> Solutions { get; init; } = [];
    public IReadOnlyCollection<Submission> Submissions { get; init; } = [];

    public virtual void Start(DateTime startedAt, ProblemSet problemSet)
    {
        if (Status != DuelStatus.Pending)
        {
            throw new InvalidOperationException("Нельзя начать дуэль, которая уже начата.");
        }

        ProblemSet = problemSet;
        Status = DuelStatus.InProgress;
        UpdatedAt = startedAt;
        StartedAt = startedAt;
        
        AddDomainEvent(new DuelStartedDomainEvent(Id));
    }

    public virtual void Finish(DateTime finishedAt, User? winner)
    {
        if (Status != DuelStatus.InProgress)
        {
            throw new InvalidOperationException("Нельзя закончить дуэль, которая не в процессе.");
        }
        
        Status = DuelStatus.Finished;
        UpdatedAt = finishedAt;
        FinishedAt = finishedAt;
        Winner = winner;
        
        AddDomainEvent(new DuelFinishedDomainEvent(Id));
    }
}

public sealed record DuelId(Guid Value) : Identity<Guid>(Value);

public enum DuelType
{
    RankedDuel = 0,
    FriendlyDuel = 1,
    GroupDuel = 2,
    TournamentDuel = 3
}

public enum DuelStatus
{
    Pending = 0,
    InProgress = 1,
    Finished = 2
}
