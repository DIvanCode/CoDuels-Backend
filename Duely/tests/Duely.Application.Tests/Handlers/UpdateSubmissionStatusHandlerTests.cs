using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class UpdateSubmissionStatusHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Updates_running_on_status_ping()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Queued);
        ctx.AddRange(u1, u2, duel, sub); await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == 100)).Status.Should().Be(SubmissionStatus.Running);
        var outboxMessages = await ctx.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outboxMessages.Should().ContainSingle();
        var payload = (SendMessagePayload)outboxMessages[0].Payload;
        payload.UserId.Should().Be(u1.Id);
        payload.Message.Should().BeOfType<SubmissionStatusUpdatedMessage>()
            .Which.Status.Should().Be(SubmissionStatus.Running);
    }

    [Fact]
    public async Task Sends_status_message_when_status_unchanged()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Running);
        ctx.AddRange(u1, u2, duel, sub); await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var outboxMessages = await ctx.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outboxMessages.Should().ContainSingle();
        var payload = (SendMessagePayload)outboxMessages[0].Payload;
        payload.Message.Should().BeOfType<SubmissionStatusUpdatedMessage>()
            .Which.Status.Should().Be(SubmissionStatus.Running);
    }

    [Fact]
    public async Task Finishes_with_verdict_and_clears_message()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Running, message: "processing");
        ctx.AddRange(u1, u2, duel, sub); await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status", Verdict = "Accepted"}, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var s = await ctx.Submissions.AsNoTracking().SingleAsync(x => x.Id == 100);
        s.Status.Should().Be(SubmissionStatus.Done);
        s.Verdict.Should().Be("Accepted");
        s.Message.Should().BeNull();

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outboxMessages.Should().HaveCount(3);
        var duelChangedMessages = outboxMessages
            .Select(message => (SendMessagePayload)message.Payload)
            .Select(payload => payload.Message)
            .OfType<DuelChangedMessage>()
            .ToList();
        duelChangedMessages.Should().HaveCount(2);
        duelChangedMessages.Should().OnlyContain(message => message.DuelId == duel.Id);

        var statusMessage = outboxMessages
            .Select(message => (SendMessagePayload)message.Payload)
            .Select(payload => payload.Message)
            .OfType<SubmissionStatusUpdatedMessage>()
            .Single();
        statusMessage.SubmissionId.Should().Be(sub.Id);
        statusMessage.DuelId.Should().Be(duel.Id);
        statusMessage.Status.Should().Be(SubmissionStatus.Done);
        statusMessage.Verdict.Should().Be("Accepted");
        statusMessage.Message.Should().BeNull();
    }

    [Fact]
    public async Task Finishes_with_technical_error_when_error_present()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Running, message: "processing");
        ctx.AddRange(u1, u2, duel, sub); await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status", Error = "boom" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var s = await ctx.Submissions.AsNoTracking().SingleAsync(x => x.Id == 100);
        s.Status.Should().Be(SubmissionStatus.Done);
        s.Verdict.Should().Be("Technical error");
        s.Message.Should().BeNull();
        var outboxMessages = await ctx.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outboxMessages.Should().ContainSingle();
        var payload = (SendMessagePayload)outboxMessages[0].Payload;
        payload.Message.Should().BeOfType<SubmissionStatusUpdatedMessage>()
            .Which.Verdict.Should().Be("Technical error");
    }
}
