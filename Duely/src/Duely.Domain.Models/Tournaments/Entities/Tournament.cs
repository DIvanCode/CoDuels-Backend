using System.Collections.ObjectModel;
using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Tournaments.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments.Entities;

public abstract class Tournament : Entity<TournamentId>
{
    public Tournament(
        TournamentId id,
        TournamentName name,
        TournamentType type,
        User createdBy,
        DateTime createdAt,
        TournamentConfiguration configuration)
        : base(id)
    {
        Name = name;
        Type = type;
        Status = TournamentStatus.New;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Configuration = configuration;
    }
    
    public TournamentName Name { get; init; }
    public TournamentType Type { get; init; }
    
    public TournamentStatus Status { get; private set; }
    public User CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    
    public TournamentConfiguration Configuration { get; init; }
    
    private readonly Collection<TournamentParticipant> _participants = [];
    public IReadOnlyCollection<TournamentParticipant> Participants => _participants.AsReadOnly();

    public void AddParticipant(User user)
    {
        if (_participants.Any(p => p.User.Id == user.Id))
        {
            throw new InvalidOperationException("Нельзя дважды добавить участника в турнир.");
        }
        
        var participant = new TournamentParticipant(this, user);
        _participants.Add(participant);
    }

    public virtual void Start()
    {
        if (Status != TournamentStatus.New)
        {
            throw new InvalidOperationException("Нельзя запустить уже запущенный ранее турнир.");
        }

        if (_participants.Count >= 2)
        {
            throw new InvalidOperationException("Турнир должен содержать хотя бы двух участников.");
        }
        
        Configuration.Build(Participants);
        
        Status = TournamentStatus.InProgress;
        
        AddDomainEvent(new TournamentStartedDomainEvent(Id));
    }
}

public sealed record TournamentId(Guid Value) : Identity<Guid>(Value);

public enum TournamentType
{
    Global = 0,
    Group = 1
}

public enum TournamentStatus
{
    New = 0,
    InProgress = 1,
    Finished = 2
}
