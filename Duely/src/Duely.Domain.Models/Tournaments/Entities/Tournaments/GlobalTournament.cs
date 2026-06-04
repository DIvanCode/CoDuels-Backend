using Duely.Domain.Models.Tournaments.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments.Entities.Tournaments;

public sealed class GlobalTournament : Tournament
{
    public GlobalTournament(
        TournamentId id,
        TournamentName name,
        User createdBy,
        DateTime createdAt,
        TournamentConfiguration configuration)
        : base(id, name, TournamentType.Global, createdBy, createdAt, configuration)
    {
        AddDomainEvent(new GlobalTournamentCreatedDomainEvent(Id));
    }

    public override void Start()
    {
        base.Start();
        
        AddDomainEvent(new GlobalTournamentStartedDomainEvent(Id));
    }
}
