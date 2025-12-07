using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using System.Linq;
using System.Text.Json;
using Duely.Application.UseCases.Payloads;

public class CheckDuelsForFinishHandlerTests : ContextBasedTest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static SendMessagePayload ReadSendPayload(OutboxMessage m)
        => JsonSerializer.Deserialize<SendMessagePayload>(m.Payload, Json)!;

    [Fact]
    public async Task Finishes_by_time_as_draw()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK", start: DateTime.UtcNow.AddMinutes(-40), deadline: DateTime.UtcNow.AddMinutes(-5));
        ctx.AddRange(u1, u2, duel); await ctx.SaveChangesAsync();

        var sender = new Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var ratingManager = new Mock<IRatingManager>();

        var handler = new CheckDuelsForFinishHandler(ctx, ratingManager.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().SingleAsync(dd => dd.Id == 10);
        d.Status.Should().Be(DuelStatus.Finished);
        d.Winner.Should().BeNull();
        d.EndTime.Should().NotBeNull();
        var messages = await ctx.Outbox.AsNoTracking()
        .Where(m => m.Type == OutboxType.SendMessage)
        .ToListAsync();

        messages.Should().HaveCount(2);

    }

    [Fact]
    public async Task Finishes_by_first_accepted_submission()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var now = DateTime.UtcNow;

        var duel = EntityFactory.MakeDuel(20, u1, u2, "TASK", start: now.AddMinutes(-10), deadline: now.AddMinutes(10));
        var s1 = EntityFactory.MakeSubmission(100, duel, u1, time: now.AddSeconds(30), status: SubmissionStatus.Done, verdict: "Accepted");
        var s2 = EntityFactory.MakeSubmission(101, duel, u2, time: now.AddSeconds(40), status: SubmissionStatus.Done, verdict: "Accepted");

        ctx.AddRange(u1, u2, duel, s1, s2); await ctx.SaveChangesAsync();

        var sender = new Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
        var ratingManager = new Mock<IRatingManager>();

        var handler = new CheckDuelsForFinishHandler(ctx,ratingManager.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().Include(x => x.Winner).SingleAsync(dd => dd.Id == 20);
        d.Status.Should().Be(DuelStatus.Finished);
        d.Winner!.Id.Should().Be(1);

        var messages = await ctx.Outbox.AsNoTracking()
        .Where(m => m.Type == OutboxType.SendMessage)
        .ToListAsync();

        messages.Should().HaveCount(2);

        foreach (var m in messages)
        {
            m.RetryUntil.Should().Be(duel.DeadlineTime.AddMinutes(5));

            var p = ReadSendPayload(m);
            p.Type.Should().Be(MessageType.DuelFinished);
            p.DuelId.Should().Be(duel.Id);
        }
    }

    [Fact]
    public async Task Does_nothing_when_nobody_ready()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(30, u1, u2, "TASK", start: DateTime.UtcNow, deadline: DateTime.UtcNow.AddMinutes(15));
        ctx.AddRange(u1, u2, duel); await ctx.SaveChangesAsync();
        
        var ratingManager = new Mock<IRatingManager>();

        var handler = new CheckDuelsForFinishHandler(ctx, ratingManager.Object);

        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().SingleAsync(d => d.Id == 30)).Status.Should().Be(DuelStatus.InProgress);
    }

    [Fact]
    public async Task Waits_if_Accepted_exists_but_earlier_submission_is_not_done()
    {
        // 18:20 — u1 отправил, статус Running
        // 18:21 — u2 отправил, статус Done, Accepted
        // Ожидание: дуэль НЕ завершается, ждём u1
        var ctx = Context;

        var baseTime = DateTime.UtcNow.Date.AddHours(18);
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(40, u1, u2, "TASK", start: baseTime.AddMinutes(-10), deadline: baseTime.AddHours(1));

        var earlierRunning = EntityFactory.MakeSubmission(200, duel, u1, time: baseTime.AddMinutes(20), status: SubmissionStatus.Running);
        var laterAccepted = EntityFactory.MakeSubmission(201, duel, u2, time: baseTime.AddMinutes(21), status: SubmissionStatus.Done, verdict: "Accepted");

        ctx.AddRange(u1, u2, duel, earlierRunning, laterAccepted);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        var handler = new CheckDuelsForFinishHandler(ctx,ratingManager.Object);

        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);
        var messages = await ctx.Outbox.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();

        messages.Should().HaveCount(0 );
        res.IsSuccess.Should().BeTrue();
        var d = await ctx.Duels.AsNoTracking().SingleAsync(x => x.Id == 40);
        d.Status.Should().Be(DuelStatus.InProgress); // не завершаем
    }

    [Fact]
    public async Task Finishes_when_Accepted_is_earliest_and_later_running_can_be_ignored()
    {
        // 18:20 — u1 отправил, статус Done, Accepted
        // 18:21 — u2 отправил, статус Running
        // Ожидание: дуэль завершается победой u1, ждать u2 не нужно
        var ctx = Context;

        var baseTime = DateTime.UtcNow.Date.AddHours(18);
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(50, u1, u2, "TASK", start: baseTime.AddMinutes(-10), deadline: baseTime.AddHours(1));

        var earliestAccepted = EntityFactory.MakeSubmission(300, duel, u1, time: baseTime.AddMinutes(20), status: SubmissionStatus.Done, verdict: "Accepted");
        var laterRunning = EntityFactory.MakeSubmission(301, duel, u2, time: baseTime.AddMinutes(21), status: SubmissionStatus.Running);

        ctx.AddRange(u1, u2, duel, earliestAccepted, laterRunning);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();

        var handler = new CheckDuelsForFinishHandler(ctx, ratingManager.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().Include(x => x.Winner).SingleAsync(x => x.Id == 50);
        d.Status.Should().Be(DuelStatus.Finished);
        d.Winner!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_finish_when_deadline_passed_but_some_submissions_still_running()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var now = DateTime.UtcNow;

        // Дуэль уже должна была завершиться по времени
        var duel = EntityFactory.MakeDuel(
            id: 40,
            u1, u2,
            taskId: "TASK-40",
            start: now.AddMinutes(-40),
            deadline: now.AddMinutes(-1) // дедлайн в прошлом
        );

        // Но есть посылка, отправленная до дедлайна, которая всё ещё тестируется
        var running = EntityFactory.MakeSubmission(
            id: 400,
            duel: duel,
            user: u1,
            time: now.AddMinutes(-2),
            status: SubmissionStatus.Running
        );

        ctx.AddRange(u1, u2, duel, running);
        await ctx.SaveChangesAsync();

        // Сообщения отправляться не должны
        var ratingManager = new Mock<IRatingManager>();
        var handler = new CheckDuelsForFinishHandler(ctx, ratingManager.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().Include(x => x.Winner).SingleAsync(dd => dd.Id == 40);
        d.Status.Should().Be(DuelStatus.InProgress, "есть ещё выполняющиеся посылки");
        d.EndTime.Should().BeNull();
        d.Winner.Should().BeNull();
    }

    [Fact]
    public async Task Finishes_even_if_running_submission_sent_after_deadline()
    {
        // Дуэль закончилась по времени (deadline прошёл),
        // но один из пользователей отправил посылку ПОСЛЕ дедлайна — её игнорируем.
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");

        var now = DateTime.UtcNow;
        var duel = EntityFactory.MakeDuel(
            id: 60,
            u1, u2,
            taskId: "TASK-60",
            start: now.AddMinutes(-30),
            deadline: now.AddMinutes(-5) // дедлайн уже прошёл
        );

        // Посылка отправлена после дедлайна (18:02)
        var lateRunning = EntityFactory.MakeSubmission(
            id: 600,
            duel: duel,
            user: u1,
            time: duel.DeadlineTime.AddMinutes(2), // позже дедлайна
            status: SubmissionStatus.Running
        );

        ctx.AddRange(u1, u2, duel, lateRunning);
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        var handler = new CheckDuelsForFinishHandler(ctx, ratingManager.Object);
        var res = await handler.Handle(new CheckDuelsForFinishCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var d = await ctx.Duels.AsNoTracking().SingleAsync(x => x.Id == 60);
        d.Status.Should().Be(DuelStatus.Finished, "посылки после дедлайна не продлевают дуэль");
        d.Winner.Should().BeNull("ни у кого нет Accepted до дедлайна");
        d.EndTime.Should().NotBeNull();


    }
}
