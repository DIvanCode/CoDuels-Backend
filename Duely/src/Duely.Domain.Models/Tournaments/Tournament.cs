using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;

namespace Duely.Domain.Models.Tournaments;

public abstract class Tournament
{
    public int Id { get; init; }
    public string Name { get; set; } = null!;
    public TournamentStatus Status { get; set; }
    public Group Group { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public TournamentMatchmakingType MatchmakingType { get; set; }
    public DuelConfiguration? DuelConfiguration { get; set; }

    public List<TournamentParticipant> Participants { get; } = [];
}

public enum TournamentStatus
{
    New = 1,
    InProgress = 2,
    Finished = 3
}

public enum TournamentMatchmakingType
{
    SingleEliminationBracket = 1
}
