using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.TournamentDuels;

public sealed class TournamentPendingDuel : PendingDuel
{
    public TournamentPendingDuel(
        PendingDuelId id,
        PendingDuelType type,
        DateTime createdAt,
        Tournament tournament,
        DuelConfiguration? configuration,
        IReadOnlyCollection<User> users)
        : base(id, type, createdAt)
    {
        Tournament = tournament;
        Configuration = configuration;
        Users = users;
    }
    
    public Tournament Tournament { get; init; }
    public DuelConfiguration? Configuration { get; init; }
    
    public IReadOnlyCollection<User> Users { get; init; }
    
    public bool IsAcceptedByUser1 { get; private set; }
    public bool IsAcceptedByUser2 { get; private set; }
}
