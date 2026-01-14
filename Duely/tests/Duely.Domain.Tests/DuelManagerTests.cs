using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerTests
{
    private static DateTime OldEnough() => DateTime.UtcNow.AddMinutes(-3);
    [Fact]
    public void Returns_null_when_less_than_two_users()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        var pair = manager.TryGetPair();

        pair.Should().BeNull();
    }

    [Fact]
    public void Picks_two_users_when_exactly_two_in_queue()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500,OldEnough());
        manager.AddUser(2, 1400,OldEnough());

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void For_three_users_picks_closest_by_rating()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500,OldEnough());
        manager.AddUser(2, 1000,OldEnough());
        manager.AddUser(3, 1510,OldEnough());

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        pair!.User1.Should().Be(1);
        pair.User2.Should().Be(3);
        var secondPair = manager.TryGetPair();
        secondPair.Should().BeNull();
    }
    [Fact]
    public void Picks_best_pair_globally_not_just_for_oldest()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500,OldEnough());
        manager.AddUser(2, 2000,OldEnough());
        manager.AddUser(3, 2051,OldEnough());

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 2, 3 });
    }
    [Fact]
    public void For_1500_1800_1100_prefers_pair_1500_and_1800()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500,OldEnough());
        manager.AddUser(2, 1800,OldEnough());
        manager.AddUser(3, 1100,OldEnough());

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();

        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 }); 
    }
    [Fact]
    public void Picks_best_pairs_for_complex_rating_set()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500,OldEnough());
        manager.AddUser(2, 1800,OldEnough());
        manager.AddUser(3, 2500,OldEnough());
        manager.AddUser(4, 1600,OldEnough());
        manager.AddUser(5, 2501,OldEnough());
        var firstPair = manager.TryGetPair();
        firstPair.Should().NotBeNull();

        var firstIds = new[] { firstPair!.User1, firstPair.User2 };
        firstIds.Should().BeEquivalentTo(new[] { 3, 5 });
        var secondPair = manager.TryGetPair();
        secondPair.Should().NotBeNull();

        var secondIds = new[] { secondPair!.User1, secondPair.User2 };
        secondIds.Should().BeEquivalentTo(new[] { 1, 4 });
        var thirdPair = manager.TryGetPair();
        thirdPair.Should().BeNull();
    }
    [Fact]
    public void Old_user_gets_bigger_window_and_can_match_farther()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        // user1 ждёт давно
        manager.AddUser(1, 1500, now.AddSeconds(-70));

        // user2 только пришёл
        manager.AddUser(2, 2100, now.AddSeconds(-1));

        // diff=600, общее окно=min(400,55)=55 , пары нет
        manager.TryGetPair().Should().BeNull();
        manager.AddUser(3, 1590, now.AddSeconds(-70));

        var pair = manager.TryGetPair();
        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 1, 3 });
    }
    [Fact]
    public void Matches_when_diff_equals_allowed_window()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        // 1 ждёт 0 сек , окно = 50
        manager.AddUser(1, 1000, now);
        manager.AddUser(2, 1050, now); // diff = 50

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 1, 2 });
    }
    [Fact]
    public void Does_not_match_when_diff_just_above_window()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        // окно = 50
        manager.AddUser(1, 1000, now);
        manager.AddUser(2, 1051, now); // diff = 51

        manager.TryGetPair().Should().BeNull();
    }
    [Fact]
    public void Matches_users_with_equal_ratings_first()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(2, 1500, OldEnough());
        manager.AddUser(3, 1510, OldEnough());

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 1, 2 });
    }
    [Fact]
    public void Skips_bad_pairs_and_finds_good_one()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;
        manager.AddUser(1, 1000, now);
        manager.AddUser(2, 1700, now);

        manager.AddUser(3, 1720, now);
        manager.AddUser(4, 1730, now);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 3, 4 });
    }
    [Fact]
    public void Fallback_does_not_match_before_timeout()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;
        manager.AddUser(1, 1000, now.AddSeconds(-10));
        manager.AddUser(2, 2000, now.AddSeconds(-10));

        manager.TryGetPair().Should().BeNull();
    }
}
