using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class DuelManagerOpponentAssignmentTests
{
    [Fact]
    public void RemoveUser_ClearsOpponentAssignedFlag()
    {
        var manager = new DuelManager();
        var now = DateTime.UtcNow;

        manager.AddUser(1, 1500, now.AddSeconds(-5), expectedOpponentId: 2);
        manager.AddUser(2, 1500, now.AddSeconds(-4), expectedOpponentId: 1);

        var before = manager.GetWaitingUsers();
        before.Should().ContainSingle(u => u.UserId == 1 && u.IsOpponentAssigned);
        before.Should().ContainSingle(u => u.UserId == 2 && u.IsOpponentAssigned);

        manager.RemoveUser(1);

        var after = manager.GetWaitingUsers();
        after.Should().ContainSingle(u => u.UserId == 2 && !u.IsOpponentAssigned);
    }
}
