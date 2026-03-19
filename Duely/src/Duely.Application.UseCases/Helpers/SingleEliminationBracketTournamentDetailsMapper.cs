using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public sealed class SingleEliminationBracketTournamentDetailsMapper : ITournamentDetailsMapper
{
    public TournamentMatchmakingType Type => TournamentMatchmakingType.SingleEliminationBracket;

    public IReadOnlyCollection<int> GetReferencedUserIds(Tournament tournament)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        var userIds = new HashSet<int>(tournament.Participants.Select(p => p.User.Id));

        foreach (var node in bracketTournament.Nodes.Where(n => n != null))
        {
            if (node!.UserId != null)
            {
                userIds.Add(node.UserId.Value);
            }

            if (node.WinnerUserId != null)
            {
                userIds.Add(node.WinnerUserId.Value);
            }
        }

        return userIds.ToList();
    }

    public IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        return bracketTournament.Nodes
            .Where(n => n?.DuelId != null)
            .Select(n => n!.DuelId!.Value)
            .Distinct()
            .ToList();
    }

    public TournamentDetailsDto MapDetails(
        Tournament tournament,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        return new TournamentDetailsDto
        {
            Tournament = TournamentDtoMapper.Map(tournament),
            SingleEliminationBracket = new SingleEliminationBracketDto
            {
                Nodes = bracketTournament.Nodes
                    .Select((node, index) => TournamentDtoMapper.MapBracketNode(
                        index,
                        node,
                        bracketTournament.Nodes,
                        usersById,
                        duelsById))
                    .ToList()
            }
        };
    }
}
