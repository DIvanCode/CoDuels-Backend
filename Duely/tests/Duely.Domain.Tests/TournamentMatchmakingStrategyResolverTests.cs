using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Tournaments;
using FluentAssertions;

namespace Duely.Domain.Tests;

public sealed class TournamentMatchmakingStrategyResolverTests
{
    [Fact]
    public void Returns_matching_strategy()
    {
        var strategy = new SingleEliminationBracketMatchmakingStrategy();
        var resolver = new TournamentMatchmakingStrategyResolver([strategy]);

        var result = resolver.GetStrategy(TournamentMatchmakingType.SingleEliminationBracket);

        result.Should().BeSameAs(strategy);
    }

    [Fact]
    public void Throws_when_strategy_is_missing()
    {
        var resolver = new TournamentMatchmakingStrategyResolver([]);

        var act = () => resolver.GetStrategy(TournamentMatchmakingType.SingleEliminationBracket);

        act.Should().Throw<InvalidOperationException>();
    }
}
