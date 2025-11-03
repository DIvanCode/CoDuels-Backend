using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class CheckDuelsForFinishHandlerTests
{
    private static Duel MakeInProgressDuel(int id, User u1, User u2, DateTime start, DateTime deadline, int taskId = 1)
        => new Duel
        {
            Id = id,
            TaskId = taskId,
            User1 = u1,
            User2 = u2,
            Status = DuelStatus.InProgress,
            StartTime = start,
            DeadlineTime = deadline
        };

    [Fact]
    public async Task Does_nothing_when_no_eligible_duels()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        var duel = MakeInProgressDuel(10, u1, u2, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        ctx.AddRange(u1, u2, duel);
        await ctx.SaveChangesAsync();

        var sender = new Mock<IMessageSender>(MockBehavior.Strict);
        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);

        var result = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ctx.Duels.SingleAsync()).Status.Should().Be(DuelStatus.InProgress);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Finishes_by_deadline_as_draw_when_no_accepted()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        var duel = MakeInProgressDuel(11, u1, u2, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-1));
        ctx.AddRange(u1, u2, duel);
        await ctx.SaveChangesAsync();

        var sender = new Mock<IMessageSender>();
        sender.Setup(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);

        var result = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var db = await ctx.Duels.Include(d => d.Winner).SingleAsync(d => d.Id == 11);
        db.Status.Should().Be(DuelStatus.Finished);
        db.Winner.Should().BeNull();

        sender.Verify(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Finishes_with_winner_when_single_accepted()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        var duel = MakeInProgressDuel(12, u1, u2, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        ctx.AddRange(u1, u2, duel);

        ctx.Submissions.Add(new Submission
        {
            Duel = duel,
            User = u1,
            Status = SubmissionStatus.Done,
            Verdict = "Accepted",
            SubmitTime = DateTime.UtcNow.AddMinutes(-10)
        });

        await ctx.SaveChangesAsync();

        var sender = new Mock<IMessageSender>();
        sender.Setup(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);

        var result = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var db = await ctx.Duels.Include(d => d.Winner).SingleAsync(d => d.Id == 12);
        db.Status.Should().Be(DuelStatus.Finished);
        db.Winner!.Id.Should().Be(u1.Id);

        sender.Verify(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Finishes_with_earlier_submitter_when_both_accepted()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        var duel = MakeInProgressDuel(13, u1, u2, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        ctx.AddRange(u1, u2, duel);

        var t = DateTime.UtcNow;
        ctx.Submissions.AddRange(
            new Submission
            {
                Duel = duel,
                User = u1,
                Status = SubmissionStatus.Done,
                Verdict = "Accepted",
                SubmitTime = t.AddMinutes(-5)
            },
            new Submission
            {
                Duel = duel,
                User = u2,
                Status = SubmissionStatus.Done,
                Verdict = "Accepted",
                SubmitTime = t.AddMinutes(-7)
            }
        );

        await ctx.SaveChangesAsync();

        var sender = new Mock<IMessageSender>();
        sender.Setup(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);

        var result = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var db = await ctx.Duels.Include(d => d.Winner).SingleAsync(d => d.Id == 13);
        db.Status.Should().Be(DuelStatus.Finished);
        db.Winner!.Id.Should().Be(u2.Id);

        sender.Verify(s => s.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
