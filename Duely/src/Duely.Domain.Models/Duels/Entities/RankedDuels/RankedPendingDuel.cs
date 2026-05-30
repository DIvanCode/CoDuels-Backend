using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.RankedDuels;

public sealed class RankedPendingDuel : PendingDuel
{
    public RankedPendingDuel(
        PendingDuelId id,
        PendingDuelType type,
        DateTime createdAt,
        User user,
        int rating)
        : base(id, type, createdAt)
    {
        User = user;
        Rating = rating;
    }
    
    public User User { get; init; }
    public int Rating { get; init; }
}
