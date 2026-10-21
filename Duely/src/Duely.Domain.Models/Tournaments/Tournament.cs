using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;

namespace Duely.Domain.Models.Tournaments;

public abstract class Tournament
{
    public int Id { get; init; }
    public string Name { get; set; } = null!;
    public TournamentStatus Status { get; set; }
    public Group? Group { get; set; }
    public string? Description { get; set; }
    public DateTime? StartsAt { get; set; }
    public int RegistrationRevision { get; set; }
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
    SingleEliminationBracket = 1,
    GroupStage = 2,
    Swiss = 3
}
