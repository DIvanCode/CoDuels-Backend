using System.ComponentModel;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class RankedDuelSearcher
{
    private RankedDuelSearcher(User user, DateTime createdAt)
    {
        User = user;
        CreatedAt = createdAt;
    }
    
    public User User { get; init; }
    public DateTime CreatedAt { get; init; }

    public static RankedDuelSearcher Create(User user, DateTime createdAt)
    {
        return new RankedDuelSearcher(user, createdAt);
    }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private RankedDuelSearcher()
    {
    }
#pragma warning restore CS8618, CS9264
}
