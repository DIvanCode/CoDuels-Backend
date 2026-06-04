using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Tournaments.Entities;

public abstract class TournamentConfiguration : ValueObject
{
    protected TournamentConfiguration(TournamentConfigurationType type, DuelConfiguration? duelConfiguration)
    {
        Type = type;
        DuelConfiguration = duelConfiguration;
    }
    
    public TournamentConfigurationType Type { get; init; }
    public DuelConfiguration? DuelConfiguration { get; init; }

    internal abstract void Build(IReadOnlyCollection<TournamentParticipant> participants);
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return DuelConfiguration;
    }
}

public enum TournamentConfigurationType
{
    SingleEliminationBracket = 0,
    GroupStage = 1
}
