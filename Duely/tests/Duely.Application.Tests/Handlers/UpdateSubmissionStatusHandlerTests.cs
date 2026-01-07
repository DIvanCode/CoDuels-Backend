using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

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

        var handler = new UpdateSubmissionStatusHandler(ctx, NullLogger<UpdateSubmissionStatusHandler>.Instance);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == 100)).Status.Should().Be(SubmissionStatus.Running);
        (await ctx.Outbox.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Finishes_with_verdict_and_clears_message()
    {
        var ctx = Context;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Running, message: "processing");
        ctx.AddRange(u1, u2, duel, sub); await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx, NullLogger<UpdateSubmissionStatusHandler>.Instance);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status", Verdict = "Accepted"}, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var s = await ctx.Submissions.AsNoTracking().SingleAsync(x => x.Id == 100);
        s.Status.Should().Be(SubmissionStatus.Done);
        s.Verdict.Should().Be("Accepted");
        s.Message.Should().BeNull();

        var outbox = await ctx.Outbox.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outbox.Should().HaveCount(2);
        foreach (var message in outbox)
        {
            var payload = JsonSerializer.Deserialize<SendMessagePayload>(message.Payload, jsonOptions)!;
            payload.Type.Should().Be(MessageType.DuelChanged);
            payload.DuelId.Should().Be(duel.Id);
        }
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

        var handler = new UpdateSubmissionStatusHandler(ctx, NullLogger<UpdateSubmissionStatusHandler>.Instance);
        var res = await handler.Handle(new UpdateSubmissionStatusCommand {
            SubmissionId = 100, Type = "status", Error = "boom" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var s = await ctx.Submissions.AsNoTracking().SingleAsync(x => x.Id == 100);
        s.Status.Should().Be(SubmissionStatus.Done);
        s.Verdict.Should().Be("Technical error");
        s.Message.Should().BeNull();
        (await ctx.Outbox.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }
}
