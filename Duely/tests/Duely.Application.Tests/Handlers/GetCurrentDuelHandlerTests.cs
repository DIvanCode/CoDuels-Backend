using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class GetCurrentDuelHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_no_current_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetRatingChanges(It.IsAny<Duel>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 10,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -10
            });

        var handler = new GetCurrentDuelHandler(ctx, ratingManager.Object);

        var res = await handler.Handle(new GetCurrentDuelQuery
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_current_duel_for_user1()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = new Duel
        {
            Id = 10,
            TaskId = "TASK-10",
            Status = DuelStatus.InProgress,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1600
        };
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetRatingChanges(duel, 1500, 1600))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 15,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -15
            });
        ratingManager.Setup(m => m.GetRatingChanges(duel, 1600, 1500))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 10,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -10
            });

        var handler = new GetCurrentDuelHandler(ctx, ratingManager.Object);

        var res = await handler.Handle(new GetCurrentDuelQuery
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(10);
        res.Value.TaskId.Should().Be("TASK-10");
        res.Value.Status.Should().Be(DuelStatus.InProgress);
        res.Value.Participants.Should().HaveCount(2);
        res.Value.Participants.Should().Contain(p => p.Id == 1 && p.Rating == 1500);
        res.Value.Participants.Should().Contain(p => p.Id == 2 && p.Rating == 1600);
        res.Value.RatingChanges.Should().ContainKey(1);
        res.Value.RatingChanges.Should().ContainKey(2);
    }

    [Fact]
    public async Task Returns_current_duel_for_user2()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetRatingChanges(It.IsAny<Duel>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 10,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = -10
            });

        var handler = new GetCurrentDuelHandler(ctx, ratingManager.Object);

        var res = await handler.Handle(new GetCurrentDuelQuery
        {
            UserId = 2
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(10);
    }

    [Fact]
    public async Task NotFound_when_duel_finished()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        duel.Status = DuelStatus.Finished;
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();

        var handler = new GetCurrentDuelHandler(ctx, ratingManager.Object);

        var res = await handler.Handle(new GetCurrentDuelQuery
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}

