using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.TournamentDuels;

public sealed class TournamentDuel : Duel
{
    public TournamentDuel(
        DuelId id,
        DuelType type,
        DateTime startTime,
        DateTime deadlineTime,
        IReadOnlyDictionary<char, Problem> problems,
        IReadOnlyCollection<User> users,
        Tournament tournament,
        DuelConfiguration? configuration,
        User createdBy)
        : base(id, type, startTime, deadlineTime, problems, users)
    {
        Tournament = tournament;
        Configuration = configuration;
    }
    
    public Tournament Tournament { get; init; }
    public DuelConfiguration? Configuration { get; init; }
}
