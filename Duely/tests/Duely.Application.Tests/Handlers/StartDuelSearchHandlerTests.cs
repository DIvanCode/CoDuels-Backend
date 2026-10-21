using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Domain.Models.Duels.Pending;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class StartDuelSearchHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new StartDuelSearchHandler(Context);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_when_user_already_in_rated_search_does_not_add_duplicate()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1400;
        ctx.Users.Add(u1);
        ctx.PendingDuels.Add(new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = u1,
            Rating = u1.Rating,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>()
            .Count(p => p.User.Id == u1.Id)
            .Should().Be(1);
    }

    [Fact]
    public async Task Success_adds_ranked_pending()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1400;
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>()
            .Should().ContainSingle(p => p.User.Id == u1.Id);

        var outbox = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Starting_search_preserves_friendly_invitation()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        inviter.Rating = 1500;
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = inviter,
            User2 = invitee,
            Configuration = null,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = inviter.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().ContainSingle(p => p.User.Id == inviter.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Starting_search_is_allowed_with_active_rated_duel()
    {
        var ctx = Context;
        var user = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(user);
        ctx.Duels.Add(new Duely.Domain.Models.Duels.Duel
        {
            Id = user.Id,
            Status = Duely.Domain.Models.Duels.DuelStatus.InProgress,
            Configuration = new Duely.Domain.Models.Duels.DuelConfiguration
            {
                Id = 1,
                Owner = null,
                IsRated = true,
                ShouldShowOpponentSolution = true,
                MaxDurationMinutes = 30,
                TasksCount = 1,
                TasksOrder = Duely.Domain.Models.Duels.DuelTasksOrder.Sequential,
                TasksConfigurations = []
            },
            Tasks = [],
            User1 = user,
            User1InitRating = user.Rating,
            User2 = user,
            User2InitRating = user.Rating,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1Solutions = [],
            User2Solutions = []
        });
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = user.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().ContainSingle();
    }
}
