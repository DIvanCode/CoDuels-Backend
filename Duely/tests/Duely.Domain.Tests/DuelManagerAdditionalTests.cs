using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerAdditionalTests
{
    private static DateTime OldEnough() => DateTime.UtcNow.AddMinutes(-3);

    [Fact]
    public void GetWaitingUsersCount_ReturnsZeroWhenEmpty()
    {
        var manager = new DuelManager();

        var count = manager.GetWaitingUsersCount();

        count.Should().Be(0);
    }

    [Fact]
    public void GetWaitingUsersCount_ReturnsCorrectCount()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(2, 1600, OldEnough());
        manager.AddUser(3, 1700, OldEnough());

        var count = manager.GetWaitingUsersCount();

        count.Should().Be(3);
    }

    [Fact]
    public void GetWaitingUsersCount_DecreasesAfterPairRemoved()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(2, 1600, OldEnough());
        manager.TryGetPair();

        var count = manager.GetWaitingUsersCount();

        count.Should().Be(0);
    }

    [Fact]
    public void AddUser_IgnoresDuplicateUser()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(1, 1600, OldEnough()); // попытка добавить того же пользователя

        var count = manager.GetWaitingUsersCount();
        count.Should().Be(1);
        
        var pair = manager.TryGetPair();
        pair.Should().BeNull(); // не может создать пару с одним пользователем
    }

    [Fact]
    public void RemoveUser_IgnoresNonExistentUser()
    {
        var manager = new DuelManager();

        manager.RemoveUser(999); // попытка удалить несуществующего пользователя

        var count = manager.GetWaitingUsersCount();
        count.Should().Be(0);
    }

    [Fact]
    public void RemoveUser_RemovesExistingUser()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(2, 1600, OldEnough());
        manager.RemoveUser(1);

        var count = manager.GetWaitingUsersCount();
        count.Should().Be(1);
        
        var pair = manager.TryGetPair();
        pair.Should().BeNull(); // не может создать пару с одним пользователем
    }

    [Fact]
    public void Fallback_MatchesAfterTimeout()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;
        
        // Пользователи ждут больше 120 секунд
        manager.AddUser(1, 1000, now.AddSeconds(-121));
        manager.AddUser(2, 2000, now.AddSeconds(-121));

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Fallback_MatchesBestPairAfterTimeout()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;
        
        // Пользователи ждут больше 120 секунд
        manager.AddUser(1, 1000, now.AddSeconds(-121));
        manager.AddUser(2, 1500, now.AddSeconds(-121));
        manager.AddUser(3, 2000, now.AddSeconds(-121));

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        // Должен выбрать пару с минимальной разницей (1 и 2, разница 500)
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Fallback_PrefersUsersWithLongerWaitTime()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;
        
        // Пользователи ждут больше 120 секунд
        manager.AddUser(1, 1000, now.AddSeconds(-130));
        manager.AddUser(2, 1050, now.AddSeconds(-125));
        manager.AddUser(3, 1100, now.AddSeconds(-130));

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        // Должен выбрать пару с минимальной разницей и большим временем ожидания
        // 1 и 2 имеют разницу 50, но 1 и 3 имеют разницу 100
        // Но 1 и 3 оба ждут дольше (130 секунд)
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 }); // минимальная разница
    }

    [Fact]
    public void TryGetPair_ReturnsNullWhenOnlyOneUserAfterRemoval()
    {
        var manager = new DuelManager();

        manager.AddUser(1, 1500, OldEnough());
        manager.AddUser(2, 1600, OldEnough());
        manager.RemoveUser(1);

        var pair = manager.TryGetPair();

        pair.Should().BeNull();
    }

    [Fact]
    public void WindowGrowsOverTime()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        // Пользователь ждёт 10 секунд, окно = 50 + 10*5 = 100
        manager.AddUser(1, 1000, now.AddSeconds(-10));
        manager.AddUser(2, 1100, now); // diff = 100, окно для 2 = 50

        // Окно для 1 = 100, для 2 = 50, минимум = 50
        // diff = 100 > 50, не должно совпасть
        var pair1 = manager.TryGetPair();
        pair1.Should().BeNull();

        // Но если 2 тоже ждёт 10 секунд, окно для 2 = 100
        manager.RemoveUser(2);
        manager.AddUser(2, 1100, now.AddSeconds(-10));
        
        // Теперь оба имеют окно 100, diff = 100 <= 100, должно совпасть
        var pair2 = manager.TryGetPair();
        pair2.Should().NotBeNull();
    }

    [Fact]
    public void TryGetPair_HandlesEqualRatingsWithDifferentWaitTimes()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1500, now.AddSeconds(-20));
        manager.AddUser(2, 1500, now.AddSeconds(-10));
        manager.AddUser(3, 1500, now);

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        // Должен выбрать пару с максимальным минимальным временем ожидания
        // 1 и 2: min(20, 10) = 10
        // 1 и 3: min(20, 0) = 0
        // 2 и 3: min(10, 0) = 0
        // Должен выбрать 1 и 2
        var ids = new[] { pair!.User1, pair.User2 };
        ids.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Fallback_ReturnsNullWhenOnlyOneUser()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1000, now.AddSeconds(-121));

        var pair = manager.TryGetPair();

        pair.Should().BeNull();
    }

    [Fact]
    public void Invited_users_match_only_when_mutual()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1500, now.AddSeconds(-5), expectedOpponentId: 2);
        manager.AddUser(2, 1500, now.AddSeconds(-4));

        manager.TryGetPair().Should().BeNull();

        manager.RemoveUser(2);
        manager.AddUser(2, 1500, now.AddSeconds(-3), expectedOpponentId: 1);

        var pair = manager.TryGetPair();
        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Invited_user_is_not_matched_in_regular_queue()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1500, now.AddSeconds(-5), expectedOpponentId: 2);
        manager.AddUser(3, 1500, now.AddSeconds(-4));
        manager.AddUser(4, 1500, now.AddSeconds(-3));

        var pair = manager.TryGetPair();

        pair.Should().NotBeNull();
        new[] { pair!.User1, pair.User2 }
            .Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public void Users_with_different_configurations_do_not_match()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1500, now.AddSeconds(-5), configurationId: 10);
        manager.AddUser(2, 1500, now.AddSeconds(-5), configurationId: 11);

        manager.TryGetPair().Should().BeNull();
    }
}

