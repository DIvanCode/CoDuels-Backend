using Duely.Domain.Models.Tournaments;

namespace Duely.Domain.Services.Tournaments;

public sealed class TournamentMatchmakingStrategyResolver(IEnumerable<ITournamentMatchmakingStrategy> strategies)
    : ITournamentMatchmakingStrategyResolver
{
    public ITournamentMatchmakingStrategy GetStrategy(TournamentMatchmakingType type)
    {
        return strategies.Single(strategy => strategy.Type == type);
    }
}
