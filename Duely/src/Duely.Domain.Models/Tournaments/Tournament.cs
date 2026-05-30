using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments;

public abstract class Tournament
{
    public int Id { get; init; }
    public required string Name { get; init; }
    
    public required TournametStatus Status { get; set; }
    public required User CreatedBy { get; init; }
    public required DateTime CreatedAt { get; init; }
    
    public required TournamentType Type { get; init; }
    public required DuelConfiguration? DuelConfiguration { get; init; }
    
    public List<TournamentParticipant> Participants { get; init; } = [];
    public List<Duel> Duels { get; init; } = [];    
}

public enum TournamentStatus
{
    New = 0,
    InProgress = 1,
    Finished = 2
}

public enum TournamentType
{
    SingleEliminationBracket = 0,
    GroupStage = 1
}
