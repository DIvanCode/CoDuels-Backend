using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class FriendlyInvitationIsolationTests : ContextBasedTest
{
    [Fact]
    public async Task Legacy_duplicate_pair_operations_select_oldest_and_preserve_other_rows()
    {
        var sender = EntityFactory.MakeUser(1, "sender");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        var other = EntityFactory.MakeUser(3, "other");
        var configuration = new DuelConfiguration
        {
            Id = 20, MaxDurationMinutes = 30, TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential, TasksConfigurations = []
        };
        Context.Users.AddRange(sender, invitee, other);
        Context.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly, User1 = sender, User2 = invitee,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly, User1 = sender, User2 = invitee,
                Configuration = configuration, CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly, User1 = sender, User2 = other,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        (await new AcceptDuelInvitationHandler(Context).Handle(
            new AcceptDuelInvitationCommand
            {
                UserId = invitee.Id, OpponentNickname = sender.Nickname,
                ConfigurationId = configuration.Id
            }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new CancelDuelInvitationHandler(Context).Handle(
            new CancelDuelInvitationCommand
            {
                UserId = sender.Id, OpponentNickname = invitee.Nickname
            }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new DenyDuelInvitationHandler(Context).Handle(
            new DenyDuelInvitationCommand
            {
                UserId = other.Id, OpponentNickname = sender.Nickname
            }, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var remaining = await Context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(duel => duel.Configuration)
            .SingleAsync();
        remaining.User1.Id.Should().Be(sender.Id);
        remaining.User2.Id.Should().Be(invitee.Id);
        remaining.Configuration!.Id.Should().Be(configuration.Id);
        remaining.IsAccepted.Should().BeFalse();
    }
}
