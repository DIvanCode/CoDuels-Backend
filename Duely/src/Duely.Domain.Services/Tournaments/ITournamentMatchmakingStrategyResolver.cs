using Duely.Domain.Models.Tournaments;

namespace Duely.Domain.Services.Tournaments;

public interface ITournamentMatchmakingStrategyResolver
{
    ITournamentMatchmakingStrategy GetStrategy(TournamentMatchmakingType type);
}
