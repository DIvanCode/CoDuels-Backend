using System;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Services;

public class DuelManagerTests
{
    [Fact]
    public void Returns_null_when_less_than_two_users()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500);
        var pair = manager.TryGetPair();

        pair.Should().BeNull();
    }

    [Fact]
    public void Picks_two_users_when_exactly_two_in_queue()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500);
        manager.AddUser(2, 1400);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        var ids = new[] { pair!.Value.User1, pair.Value.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void For_three_users_picks_closest_by_rating()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500);
        manager.AddUser(2, 1000);
        manager.AddUser(3, 1510);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        pair!.Value.User1.Should().Be(1);
        pair.Value.User2.Should().Be(3);
        var secondPair = manager.TryGetPair();
        secondPair.Should().BeNull();
    }
    [Fact]
    public void Picks_best_pair_globally_not_just_for_oldest()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500);
        manager.AddUser(2, 2000);
        manager.AddUser(3, 2001);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        var ids = new[] { pair!.Value.User1, pair.Value.User2 };
        ids.Should().BeEquivalentTo(new[] { 2, 3 });
    }
    [Fact]
    public void For_1500_1800_1100_prefers_pair_1500_and_1800()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500);
        manager.AddUser(2, 1800);
        manager.AddUser(3, 1100);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();

        var ids = new[] { pair!.Value.User1, pair.Value.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 }); 
    }
    [Fact]
    public void Picks_best_pairs_for_complex_rating_set()
    {
        var manager = new DuelManager();
        manager.AddUser(1, 1500);
        manager.AddUser(2, 1800);
        manager.AddUser(3, 2500);
        manager.AddUser(4, 1510);
        manager.AddUser(5, 2501);
        var firstPair = manager.TryGetPair();
        firstPair.Should().NotBeNull();

        var firstIds = new[] { firstPair!.Value.User1, firstPair.Value.User2 };
        firstIds.Should().BeEquivalentTo(new[] { 3, 5 });
        var secondPair = manager.TryGetPair();
        secondPair.Should().NotBeNull();

        var secondIds = new[] { secondPair!.Value.User1, secondPair.Value.User2 };
        secondIds.Should().BeEquivalentTo(new[] { 1, 4 });
        var thirdPair = manager.TryGetPair();
        thirdPair.Should().BeNull();
    }
}
