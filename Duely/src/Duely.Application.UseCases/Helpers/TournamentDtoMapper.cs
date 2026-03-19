using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public static class TournamentDtoMapper
{
    public static TournamentDto Map(Tournament tournament)
    {
        return new TournamentDto
        {
            Id = tournament.Id,
            Name = tournament.Name,
            Status = tournament.Status,
            GroupId = tournament.Group.Id,
            CreatedAt = tournament.CreatedAt,
            CreatedBy = MapUser(tournament.CreatedBy),
            Participants = tournament.Participants
                .OrderBy(p => p.Seed)
                .Select(p => MapUser(p.User))
                .ToList(),
            MatchmakingType = tournament.MatchmakingType,
            DuelConfigurationId = tournament.DuelConfiguration?.Id
        };
    }

    private static UserDto MapUser(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            Rating = user.Rating,
            CreatedAt = user.CreatedAt
        };
    }

    public static SingleEliminationBracketNodeDto? MapBracketNode(
        int index,
        SingleEliminationBracketNode? node,
        IReadOnlyList<SingleEliminationBracketNode?> nodes,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        if (node == null)
        {
            return null;
        }

        var leftChildIndex = index * 2 + 1;
        var rightChildIndex = index * 2 + 2;
        int? leftIndex = leftChildIndex < nodes.Count && nodes[leftChildIndex] != null ? leftChildIndex : null;
        int? rightIndex = rightChildIndex < nodes.Count && nodes[rightChildIndex] != null ? rightChildIndex : null;

        return new SingleEliminationBracketNodeDto
        {
            Index = index,
            User = node.UserId != null && usersById.TryGetValue(node.UserId.Value, out var user) ? MapUser(user) : null,
            Winner = node.WinnerUserId != null && usersById.TryGetValue(node.WinnerUserId.Value, out var winner)
                ? MapUser(winner)
                : null,
            DuelId = node.DuelId,
            DuelStatus = node.DuelId != null && duelsById.TryGetValue(node.DuelId.Value, out var duel)
                ? duel.Status
                : null,
            LeftIndex = leftIndex,
            RightIndex = rightIndex
        };
    }
}
