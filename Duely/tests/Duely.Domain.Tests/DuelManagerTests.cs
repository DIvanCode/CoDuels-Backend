using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerTests
{
    private static User MakeUser(int id, int rating)
    {
        return new User
        {
            Id = id,
            Nickname = $"u{id}",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void Returns_empty_when_less_than_two_ranked()
    {
        var manager = new DuelManager();
        var user = MakeUser(1, 1500);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user,
                Rating = user.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            }
        };

        var pairs = manager.GetPairs(pending);

        pairs.Should().BeEmpty();
    }

    [Fact]
    public void Picks_two_users_when_exactly_two_in_queue()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1500);
        var u2 = MakeUser(2, 1400);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            }
        };

        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        var ids = new[] { pairs[0].User1.Id, pairs[0].User2.Id };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
        pairs[0].IsRated.Should().BeTrue();
    }

    [Fact]
    public void For_three_users_picks_closest_by_rating()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1500);
        var u2 = MakeUser(2, 1000);
        var u3 = MakeUser(3, 1510);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u3,
                Rating = u3.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            }
        };

        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        pairs[0].User1.Id.Should().Be(1);
        pairs[0].User2.Id.Should().Be(3);
    }

    [Fact]
    public void Fallback_matches_after_timeout()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1000);
        var u2 = MakeUser(2, 2000);

        var now = DateTime.UtcNow.AddSeconds(-121);
        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = now
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = now
            }
        };

        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        var ids = new[] { pairs[0].User1.Id, pairs[0].User2.Id };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Friendly_invitation_requires_acceptance()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1200);
        var u2 = MakeUser(2, 1300);

        var pending = new List<PendingDuel>
        {
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = u1,
                User2 = u2,
                Configuration = new DuelConfiguration { Id = 10, IsRated = false, MaxDurationMinutes = 30, TasksCount = 1, TasksOrder = DuelTasksOrder.Sequential, TasksConfigurations = [] },
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        manager.GetPairs(pending).Should().BeEmpty();

        ((FriendlyPendingDuel)pending[0]).IsAccepted = true;
        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        pairs[0].Configuration?.Id.Should().Be(10);
        pairs[0].IsRated.Should().BeFalse();
    }

    [Fact]
    public void Does_not_match_when_diff_above_window_before_timeout()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1000);
        var u2 = MakeUser(2, 1200);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow
            }
        };

        manager.GetPairs(pending).Should().BeEmpty();
    }

    [Fact]
    public void Returns_only_one_ranked_pair_even_when_more_available()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1000);
        var u2 = MakeUser(2, 1001);
        var u3 = MakeUser(3, 2000);
        var u4 = MakeUser(4, 2001);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u1, Rating = u1.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u2, Rating = u2.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u3, Rating = u3.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u4, Rating = u4.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) }
        };

        var pairs = manager.GetPairs(pending).ToList();
        pairs.Should().HaveCount(1);
    }
}
