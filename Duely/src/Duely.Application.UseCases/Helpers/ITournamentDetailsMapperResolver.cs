using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public interface ITournamentDetailsMapperResolver
{
    ITournamentDetailsMapper GetMapper(TournamentMatchmakingType type);
}
