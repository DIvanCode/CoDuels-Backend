using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public sealed class SwissTournamentDetailsMapper : ITournamentDetailsMapper
{
    public TournamentMatchmakingType Type => TournamentMatchmakingType.Swiss;

    public IReadOnlyCollection<int> GetReferencedUserIds(Tournament tournament)
        => tournament.Participants.Select(p => p.User.Id).ToList();

    public IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament) => [];

    public TournamentDetailsDto MapDetails(
        Tournament tournament,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        return new TournamentDetailsDto
        {
            Tournament = TournamentDtoMapper.Map(tournament),
            Swiss = new SwissTournamentDetailsDto
            {
                Standings = tournament.Participants
                    .OrderBy(p => p.Seed)
                    .Select(p => new SwissTournamentStandingDto
                    {
                        User = TournamentDtoMapper.MapUser(p.User),
                        Points = 0
                    })
                    .ToList()
            }
        };
    }
}
