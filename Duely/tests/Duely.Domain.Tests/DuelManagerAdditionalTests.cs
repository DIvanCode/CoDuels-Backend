using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerAdditionalTests
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
    public void Returns_single_pair_from_ranked_queue()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1000);
        var u2 = MakeUser(2, 1010);
        var u3 = MakeUser(3, 2000);
        var u4 = MakeUser(4, 2010);

        var pending = new List<PendingDuel>
        {
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u1, Rating = u1.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u2, Rating = u2.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u3, Rating = u3.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new RankedPendingDuel { Type = PendingDuelType.Ranked, User = u4, Rating = u4.Rating, CreatedAt = DateTime.UtcNow.AddMinutes(-3) }
        };

        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        pairs.Should().OnlyContain(p => p.IsRated);
    }

    [Fact]
    public void Group_duel_requires_both_acceptances()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1000);
        var u2 = MakeUser(2, 1000);

        var pending = new List<PendingDuel>
        {
            new GroupPendingDuel
            {
                Type = PendingDuelType.Group,
                Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "g" },
                CreatedBy = u1,
                User1 = u1,
                User2 = u2,
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        manager.GetPairs(pending).Should().BeEmpty();

        ((GroupPendingDuel)pending[0]).IsAcceptedByUser2 = true;
        var pairs = manager.GetPairs(pending).ToList();

        pairs.Should().HaveCount(1);
        pairs[0].IsRated.Should().BeFalse();
    }

    [Fact]
    public void Friendly_pair_blocks_ranked_for_same_users()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1500);
        var u2 = MakeUser(2, 1500);

        var pending = new List<PendingDuel>
        {
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = u1,
                User2 = u2,
                Configuration = null,
                IsAccepted = true,
                CreatedAt = DateTime.UtcNow
            },
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
        pairs[0].IsRated.Should().BeFalse();
    }
}
