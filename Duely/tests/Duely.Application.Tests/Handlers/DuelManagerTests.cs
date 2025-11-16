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
        pair!.Value.User1.Should().Be(1);
        pair.Value.User2.Should().Be(2);
    }

    [Fact]
    public void For_three_users_picks_closest_by_rating_for_oldest_user()
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
}
