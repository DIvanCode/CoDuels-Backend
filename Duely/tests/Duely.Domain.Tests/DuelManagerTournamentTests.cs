using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Duels;
using FluentAssertions;

namespace Duely.Domain.Tests;

public sealed class DuelManagerTournamentTests
{
    [Fact]
    public void Tournament_pending_duel_is_returned_as_pair()
    {
        var user1 = new Duely.Domain.Models.User
        {
            Id = 1,
            Nickname = "u1",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = 1500,
            CreatedAt = DateTime.UtcNow
        };
        var user2 = new Duely.Domain.Models.User
        {
            Id = 2,
            Nickname = "u2",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = 1500,
            CreatedAt = DateTime.UtcNow
        };
        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "g" },
            CreatedBy = user1,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        var pending = new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new DuelManager();
        var pairs = manager.GetPairs(new List<PendingDuel> { pending }).ToList();

        pairs.Should().ContainSingle();
        pairs[0].User1.Should().BeSameAs(user1);
        pairs[0].User2.Should().BeSameAs(user2);
        pairs[0].UsedPendingDuels.Should().ContainSingle().Which.Should().BeSameAs(pending);
    }
}
