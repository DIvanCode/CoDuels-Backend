using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Duels;
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
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new DuelManager();
        var pairs = manager.GetPairs(new List<PendingDuel> { pending }).ToList();

        pairs.Should().ContainSingle();
        pairs[0].User1.Should().BeSameAs(user1);
        pairs[0].User2.Should().BeSameAs(user2);
        pairs[0].UsedPendingDuels.Should().ContainSingle().Which.Should().BeSameAs(pending);
    }

    [Fact]
    public void Tournament_pending_duel_requires_both_acceptances()
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
            IsAcceptedByUser1 = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new DuelManager();
        var pairs = manager.GetPairs(new List<PendingDuel> { pending }).ToList();

        pairs.Should().BeEmpty();
    }

    [Fact]
    public void Bot_can_be_selected_for_tournament_and_ranked_pairs_in_same_tick()
    {
        var tournamentHuman = MakeUser(1, "tournament-human");
        var rankedHuman = MakeUser(2, "ranked-human");
        var bot = MakeUser(3, "bot", isBot: true);
        var tournament = new GroupStageTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "group" },
            CreatedBy = tournamentHuman,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.GroupStage
        };
        var pending = new List<PendingDuel>
        {
            new TournamentPendingDuel
            {
                Id = 1,
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = tournamentHuman,
                User2 = bot,
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow
            },
            new RankedPendingDuel
            {
                Id = 2,
                Type = PendingDuelType.Ranked,
                User = rankedHuman,
                Rating = rankedHuman.Rating,
                CreatedAt = DateTime.UtcNow.AddSeconds(-10)
            },
            new RankedPendingDuel
            {
                Id = 3,
                Type = PendingDuelType.Ranked,
                User = bot,
                Rating = bot.Rating,
                CreatedAt = DateTime.UtcNow
            }
        };

        var pairs = new DuelManager().GetPairs(pending).ToList();

        pairs.Should().HaveCount(2);
        pairs[0].UsedPendingDuels.Should().ContainSingle().Which.Should().BeOfType<TournamentPendingDuel>();
        pairs[1].UsedPendingDuels.Should().HaveCount(2);
        pairs[1].User2.Should().BeSameAs(bot);
    }

    [Fact]
    public void Bot_is_not_selected_for_two_matches_of_the_same_tournament_in_one_tick()
    {
        var human1 = MakeUser(1, "human1");
        var human2 = MakeUser(2, "human2");
        var bot = MakeUser(3, "bot", isBot: true);
        var tournament = new GroupStageTournament
        {
            Id = 4,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "group" },
            CreatedBy = human1,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.GroupStage
        };
        var pending = new List<PendingDuel>
        {
            MakeTournamentPending(1, tournament, human1, bot),
            MakeTournamentPending(2, tournament, human2, bot)
        };

        new DuelManager().GetPairs(pending).Should().ContainSingle();
    }

    [Fact]
    public void Unrated_pairs_can_share_a_human_with_a_rated_pair()
    {
        var human = MakeUser(1, "human");
        var opponents = Enumerable.Range(2, 4).Select(id => MakeUser(id, $"u{id}")).ToArray();
        var tournament = new GroupStageTournament
        {
            Id = 10,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "group" },
            CreatedBy = human,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.GroupStage
        };
        var pending = new List<PendingDuel>
        {
            new FriendlyPendingDuel
            {
                Id = 1, Type = PendingDuelType.Friendly, User1 = human, User2 = opponents[0],
                Configuration = null,
                IsAccepted = true, CreatedAt = DateTime.UtcNow
            },
            new GroupPendingDuel
            {
                Id = 2, Type = PendingDuelType.Group, Group = tournament.Group,
                CreatedBy = human, User1 = human, User2 = opponents[1],
                Configuration = null,
                IsAcceptedByUser1 = true, IsAcceptedByUser2 = true, CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Id = 3, Type = PendingDuelType.Tournament, Tournament = tournament,
                User1 = human, User2 = opponents[2],
                Configuration = new DuelConfiguration { IsRated = true },
                IsAcceptedByUser1 = true, IsAcceptedByUser2 = true, CreatedAt = DateTime.UtcNow
            },
            new RankedPendingDuel
            {
                Id = 4, Type = PendingDuelType.Ranked, User = human,
                Rating = human.Rating, CreatedAt = DateTime.UtcNow.AddSeconds(-10)
            },
            new RankedPendingDuel
            {
                Id = 5, Type = PendingDuelType.Ranked, User = opponents[3],
                Rating = opponents[3].Rating, CreatedAt = DateTime.UtcNow.AddSeconds(-10)
            }
        };

        var pairs = new DuelManager().GetPairs(pending).ToList();

        pairs.Should().HaveCount(3);
        pairs.Select(pair => pair.IsRated).Should().Equal(false, false, true);
        pairs.Should().NotContain(pair => pair.UsedPendingDuels.Any(duel => duel.Type == PendingDuelType.Ranked));
    }

    [Fact]
    public void Only_one_rated_configuration_pair_is_selected_for_each_human()
    {
        var human = MakeUser(1, "human");
        var opponents = Enumerable.Range(2, 3).Select(id => MakeUser(id, $"u{id}")).ToArray();
        var tournament = new GroupStageTournament
        {
            Id = 10, Name = "Cup", Status = TournamentStatus.InProgress,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "group" },
            CreatedBy = human, CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.GroupStage
        };
        var ratedConfiguration = new DuelConfiguration { IsRated = true };
        var pending = new List<PendingDuel>
        {
            new FriendlyPendingDuel
            {
                Id = 1, Type = PendingDuelType.Friendly, User1 = human, User2 = opponents[0],
                Configuration = ratedConfiguration, IsAccepted = true, CreatedAt = DateTime.UtcNow
            },
            new GroupPendingDuel
            {
                Id = 2, Type = PendingDuelType.Group, Group = tournament.Group,
                CreatedBy = human, User1 = human, User2 = opponents[1],
                Configuration = ratedConfiguration,
                IsAcceptedByUser1 = true, IsAcceptedByUser2 = true, CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Id = 3, Type = PendingDuelType.Tournament, Tournament = tournament,
                User1 = human, User2 = opponents[2], Configuration = ratedConfiguration,
                IsAcceptedByUser1 = true, IsAcceptedByUser2 = true, CreatedAt = DateTime.UtcNow
            }
        };

        var pairs = new DuelManager().GetPairs(pending).ToList();

        pairs.Should().ContainSingle().Which.UsedPendingDuels.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    private static TournamentPendingDuel MakeTournamentPending(
        int id,
        Tournament tournament,
        Duely.Domain.Models.User user1,
        Duely.Domain.Models.User user2) => new()
    {
        Id = id,
        Type = PendingDuelType.Tournament,
        Tournament = tournament,
        User1 = user1,
        User2 = user2,
        Configuration = null,
        IsAcceptedByUser1 = true,
        IsAcceptedByUser2 = true,
        CreatedAt = DateTime.UtcNow
    };

    private static Duely.Domain.Models.User MakeUser(int id, string nickname, bool isBot = false) => new()
    {
        Id = id,
        Nickname = nickname,
        PasswordSalt = "salt",
        PasswordHash = "hash",
        Rating = 1500,
        CreatedAt = DateTime.UtcNow,
        IsBot = isBot
    };
}
