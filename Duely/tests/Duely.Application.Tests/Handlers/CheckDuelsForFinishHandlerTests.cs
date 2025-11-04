using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class CheckDuelsForFinishHandlerTests
{
    [Fact]
    public async Task Finishes_by_time_as_draw()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK", start: DateTime.UtcNow.AddMinutes(-40), deadline: DateTime.UtcNow.AddMinutes(-5));
        ctx.AddRange(u1, u2, duel); await ctx.SaveChangesAsync();

        var sender = new Moq.Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().SingleAsync(dd => dd.Id == 10);
        d.Status.Should().Be(DuelStatus.Finished);
        d.Winner.Should().BeNull();
        d.EndTime.Should().NotBeNull();

        sender.VerifyAll();
    }

    [Fact]
    public async Task Finishes_by_first_accepted_submission()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(20, u1, u2, "TASK", start: DateTime.UtcNow.AddMinutes(-10), deadline: DateTime.UtcNow.AddMinutes(10));
        var s1 = EntityFactory.MakeSubmission(100, duel, u1, time: DateTime.UtcNow.AddSeconds(30), status: SubmissionStatus.Done, verdict: "Accepted");
        var s2 = EntityFactory.MakeSubmission(101, duel, u2, time: DateTime.UtcNow.AddSeconds(40), status: SubmissionStatus.Done, verdict: "Accepted");

        ctx.AddRange(u1, u2, duel, s1, s2); await ctx.SaveChangesAsync();

        var sender = new Moq.Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().Include(x => x.Winner).SingleAsync(dd => dd.Id == 20);
        d.Status.Should().Be(DuelStatus.Finished);
        d.Winner!.Id.Should().Be(1);

        sender.VerifyAll();
    }

    [Fact]
    public async Task Does_nothing_when_nobody_ready()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(30, u1, u2, "TASK", start: DateTime.UtcNow, deadline: DateTime.UtcNow.AddMinutes(15));
        ctx.AddRange(u1, u2, duel); await ctx.SaveChangesAsync();

        var sender = new Moq.Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>(MockBehavior.Strict);
        var handler = new CheckDuelsForFinishHandler(ctx, sender.Object);

        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().SingleAsync(d => d.Id == 30)).Status.Should().Be(DuelStatus.InProgress);
        sender.VerifyNoOtherCalls();
    }
}
