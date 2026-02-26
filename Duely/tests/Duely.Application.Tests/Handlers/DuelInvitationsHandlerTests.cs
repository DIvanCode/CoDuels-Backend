using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Duels.Pending;
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
        ctx.Users.AddRange(u1, u2, u3);
        var createdAt1 = DateTime.UtcNow.AddMinutes(-5);
        var createdAt2 = DateTime.UtcNow.AddMinutes(-3);
        ctx.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = u2,
                User2 = u1,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = createdAt1
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = u3,
                User2 = u1,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = createdAt2
            });
        await ctx.SaveChangesAsync();

        var handler = new GetIncomingDuelInvitationsHandler(ctx);
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
        res.Value[0].Type.Should().Be(PendingDuelType.Friendly);
        res.Value[1].Type.Should().Be(PendingDuelType.Friendly);
        res.Value[0].CreatedAt.Should().Be(createdAt1);
        res.Value[1].CreatedAt.Should().Be(createdAt2);
    }

    [Fact]
    public async Task Get_incoming_invitations_skips_accepted()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u2,
            User2 = u1,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new GetIncomingDuelInvitationsHandler(ctx);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_incoming_invitations_returns_group_pending_from_pending_duels()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "g" },
            CreatedBy = u1,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAcceptedByUser1 = false,
            IsAcceptedByUser2 = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new GetIncomingDuelInvitationsHandler(ctx);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().ContainSingle();
        res.Value[0].Type.Should().Be(PendingDuelType.Group);
    }

    [Fact]
    public async Task Get_incoming_invitations_skips_group_already_accepted_by_user()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new GroupPendingDuel
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
        });
        await ctx.SaveChangesAsync();

        var handler = new GetIncomingDuelInvitationsHandler(ctx);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_incoming_invitations_returns_group_pending_for_second_user()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = new Duely.Domain.Models.Groups.Group { Id = 1, Name = "g" },
            CreatedBy = u1,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAcceptedByUser1 = false,
            IsAcceptedByUser2 = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new GetIncomingDuelInvitationsHandler(ctx);
        var res = await handler.Handle(new GetIncomingDuelInvitationsQuery
        {
            UserId = u2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().ContainSingle();
        res.Value[0].OpponentNickname.Should().Be(u1.Nickname);
        res.Value[0].Type.Should().Be(PendingDuelType.Group);
    }
}
