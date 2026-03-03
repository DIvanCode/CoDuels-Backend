using Duely.Domain.Models;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerOpponentAssignmentTests
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
    public void UsedPendingDuels_are_reported_in_pair()
    {
        var manager = new DuelManager();
        var u1 = MakeUser(1, 1500);
        var u2 = MakeUser(2, 1500);

        var pending = new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        };

        var pairs = manager.GetPairs(new List<PendingDuel> { pending }).ToList();

        pairs.Should().HaveCount(1);
        pairs[0].UsedPendingDuels.Should().ContainSingle().Which.Should().BeSameAs(pending);
    }
}
