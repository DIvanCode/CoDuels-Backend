using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.RankedDuels;

public sealed class RankedDuel : Duel
{
    public RankedDuel(
        DuelId id,
        DuelType type,
        DateTime startTime,
        DateTime deadlineTime,
        IReadOnlyDictionary<char, Problem> problems,
        IReadOnlyCollection<User> users,
        IReadOnlyDictionary<UserId, int> initRatings,
        IReadOnlyDictionary<UserId, int> finalRatings)
        : base(id, type, startTime, deadlineTime, problems, users)
    {
        InitRatings = initRatings;
        FinalRatings = finalRatings;
    }
    
    public IReadOnlyDictionary<UserId, int> InitRatings { get; init; }
    public IReadOnlyDictionary<UserId, int> FinalRatings { get; init; }
}
