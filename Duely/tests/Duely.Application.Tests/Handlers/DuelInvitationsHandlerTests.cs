using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Services.Duels;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public class DuelInvitationsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Get_incoming_invitations_returns_waiting_users()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        u1.Rating = 1500;
        u2.Rating = 1600;
        u3.Rating = 1700;
        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var duelManager = new DuelManager();
        duelManager.AddUser(u2.Id, u2.Rating, now.AddSeconds(-10), expectedOpponentId: u1.Id);
        duelManager.AddUser(u3.Id, u3.Rating, now.AddSeconds(-5), expectedOpponentId: u1.Id);
        duelManager.AddUser(4, 1500, now.AddSeconds(-2));

        var handler = new GetIncomingDuelInvitationsHandler(ctx, duelManager);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().HaveCount(2);
        res.Value[0].OpponentNickname.Should().Be(u2.Nickname);
        res.Value[1].OpponentNickname.Should().Be(u3.Nickname);
        res.Value[0].ConfigurationId.Should().BeNull();
        res.Value[1].ConfigurationId.Should().BeNull();
    }

    [Fact]
    public async Task Get_incoming_invitations_skips_assigned_opponents()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var duelManager = new DuelManager();
        duelManager.AddUser(u2.Id, u2.Rating, now.AddSeconds(-5), expectedOpponentId: u1.Id);
        duelManager.AddUser(u1.Id, u1.Rating, now.AddSeconds(-4), expectedOpponentId: u2.Id);

        var handler = new GetIncomingDuelInvitationsHandler(ctx, duelManager);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().BeEmpty();
    }
}
