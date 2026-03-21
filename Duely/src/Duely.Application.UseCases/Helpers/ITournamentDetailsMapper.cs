using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public interface ITournamentDetailsMapper
{
    TournamentMatchmakingType Type { get; }
    IReadOnlyCollection<int> GetReferencedUserIds(Tournament tournament);
    IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament);
    TournamentDetailsDto MapDetails(
        Tournament tournament,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, Duel> duelsById);
}
