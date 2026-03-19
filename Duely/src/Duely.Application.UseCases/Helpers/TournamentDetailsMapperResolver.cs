using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public sealed class TournamentDetailsMapperResolver(IEnumerable<ITournamentDetailsMapper> mappers)
    : ITournamentDetailsMapperResolver
{
    public ITournamentDetailsMapper GetMapper(TournamentMatchmakingType type)
    {
        return mappers.Single(mapper => mapper.Type == type);
    }
}
