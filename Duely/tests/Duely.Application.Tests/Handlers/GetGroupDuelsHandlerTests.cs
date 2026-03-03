using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Moq;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class GetGroupDuelsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_group_duels_and_pending_duels_ordered()
    {
        var viewer = EntityFactory.MakeUser(1, "viewer");
        var creator = EntityFactory.MakeUser(2, "creator");
        var user1 = EntityFactory.MakeUser(3, "user1");
        var user2 = EntityFactory.MakeUser(4, "user2");
        var user3 = EntityFactory.MakeUser(5, "user3");
        var user4 = EntityFactory.MakeUser(6, "user4");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = viewer,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });

        var duelStart = DateTime.UtcNow.AddMinutes(-10);
        var duel = EntityFactory.MakeDuel(1, user1, user2, start: duelStart);
        var groupDuel = new GroupDuel
        {
            Group = group,
            Duel = duel,
            CreatedBy = creator
        };
        group.Duels.Add(groupDuel);

        var pendingCreatedAt = DateTime.UtcNow.AddMinutes(-1);
        var pending = new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = creator,
            User1 = user3,
            User2 = user4,
            CreatedAt = pendingCreatedAt,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = false
        };

        Context.Users.AddRange(viewer, creator, user1, user2, user3, user4);
        Context.Groups.Add(group);
        Context.Duels.Add(duel);
        Context.Add(groupDuel);
        Context.PendingDuels.Add(pending);
        await Context.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetRatingChanges(It.IsAny<Duel>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 10,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -10
            });

        var handler = new GetGroupDuelsHandler(
            Context,
            new GroupPermissionsService(),
            ratingManager.Object,
            new TaskService());
        var res = await handler.Handle(new GetGroupDuelsQuery
        {
            UserId = viewer.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().HaveCount(2);
        res.Value[0].Duel.Should().BeNull();
        res.Value[0].User1.Id.Should().Be(user3.Id);
        res.Value[0].User1.Nickname.Should().Be(user3.Nickname);
        res.Value[0].User2.Id.Should().Be(user4.Id);
        res.Value[0].User2.Nickname.Should().Be(user4.Nickname);
        res.Value[0].IsAcceptedByUser1.Should().BeTrue();
        res.Value[0].IsAcceptedByUser2.Should().BeFalse();
        res.Value[1].Duel.Should().NotBeNull();
        res.Value[1].Duel!.Id.Should().Be(duel.Id);
        res.Value[1].User1.Id.Should().Be(user1.Id);
        res.Value[1].User1.Nickname.Should().Be(user1.Nickname);
        res.Value[1].User2.Id.Should().Be(user2.Id);
        res.Value[1].User2.Nickname.Should().Be(user2.Nickname);
        res.Value[1].IsAcceptedByUser1.Should().BeTrue();
        res.Value[1].IsAcceptedByUser2.Should().BeTrue();
    }

    [Fact]
    public async Task Forbidden_when_user_is_invited()
    {
        var invited = EntityFactory.MakeUser(1, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true
        });

        Context.Users.Add(invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetRatingChanges(It.IsAny<Duel>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 10,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -10
            });

        var handler = new GetGroupDuelsHandler(
            Context,
            new GroupPermissionsService(),
            ratingManager.Object,
            new TaskService());
        var res = await handler.Handle(new GetGroupDuelsQuery
        {
            UserId = invited.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
