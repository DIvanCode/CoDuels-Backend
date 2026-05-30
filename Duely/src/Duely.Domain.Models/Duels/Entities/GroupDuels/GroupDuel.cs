using Duely.Domain.Common;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.GroupDuels;

public sealed class GroupDuel : Duel
{
    public GroupDuel(
        DuelId id,
        DuelType type,
        DateTime startTime,
        DateTime deadlineTime,
        IReadOnlyDictionary<char, Problem> problems,
        IReadOnlyCollection<User> users,
        Group group,
        DuelConfiguration? configuration,
        User createdBy)
        : base(id, type, startTime, deadlineTime, problems, users)
    {
        Group = group;
        Configuration = configuration;
        CreatedBy = createdBy;
    }
    
    public required Group Group { get; init; }
    public required DuelConfiguration? Configuration { get; init; }
    public required User CreatedBy { get; init; }
}
