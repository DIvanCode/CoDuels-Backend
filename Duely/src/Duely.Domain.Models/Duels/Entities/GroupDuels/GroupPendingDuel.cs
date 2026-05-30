using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.GroupDuels;

public sealed class GroupPendingDuel : PendingDuel
{
    public GroupPendingDuel(
        PendingDuelId id,
        PendingDuelType type,
        DateTime createdAt,
        Group group,
        DuelConfiguration? configuration,
        User createdBy,
        IReadOnlyCollection<User> users)
        : base(id, type, createdAt)
    {
        Group = group;
        Configuration = configuration;
        CreatedBy = createdBy;
        Users = users;
    }
    
    public Group Group { get; init; }
    public DuelConfiguration? Configuration { get; init; }
    public User CreatedBy { get; init; }
    
    public IReadOnlyCollection<User> Users { get; init; }
    
    public bool IsAcceptedByUser1 { get; set; }
    public bool IsAcceptedByUser2 { get; set; }
}
