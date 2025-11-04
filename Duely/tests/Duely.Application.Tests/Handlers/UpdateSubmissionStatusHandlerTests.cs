using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class UpdateSubmissionStatusHandlerTests
{
    private static (Context ctx, System.Data.Common.DbConnection conn) NewCtx()
        => DbContextFactory.CreateSqliteContext();

    private static Submission MakeSubmission(int id, int duelId = 1, int userId = 10, SubmissionStatus status = SubmissionStatus.Queued)
        => new Submission
        {
            Id = id,
            DuelId = duelId,
            UserId = userId,
            Code = "code",
            Language = "py",
            Status = status,
            SubmitTime = DateTime.UtcNow.AddMinutes(-1)
        };

    [Fact]
    public async Task Returns_NotFound_when_submission_absent()
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = 999,
            Type = "status"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_Ok_and_does_not_change_when_already_Done()
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(1, status: SubmissionStatus.Done);
        sub.Verdict = "Accepted";
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = "status",
            Message = "ignored",
            Error = "ignored",
            Verdict = "Wrong"
        };

        var before = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);
        var result = await handler.Handle(cmd, CancellationToken.None);
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);

        result.IsSuccess.Should().BeTrue();
        after.Status.Should().Be(before.Status);
        after.Verdict.Should().Be(before.Verdict);
        after.Message.Should().Be(before.Message);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("status")]
    public async Task Sets_Running_when_type_is_start_or_status(string type)
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(2, status: SubmissionStatus.Queued);
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = type
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);
        after.Status.Should().Be(SubmissionStatus.Running);
    }

    [Fact]
    public async Task Updates_Message_only_when_provided()
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(3, status: SubmissionStatus.Running);
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = "noop",
            Message = "compiling..."
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);
        after.Status.Should().Be(SubmissionStatus.Running); // статус не меняется
        after.Message.Should().Be("compiling...");
    }

    [Fact]
    public async Task Sets_Done_TechnicalError_and_clears_Message_when_Error_present()
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(4, status: SubmissionStatus.Running);
        sub.Message = "previous";
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = "status",
            Error = "oops"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);
        after.Status.Should().Be(SubmissionStatus.Done);
        after.Verdict.Should().Be("Technical error");
        after.Message.Should().BeNull();
    }

    [Fact]
    public async Task Sets_Done_with_given_Verdict_and_clears_Message_when_Verdict_present()
    {
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(5, status: SubmissionStatus.Running);
        sub.Message = "previous";
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = "status",
            Verdict = "Accepted"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);
        after.Status.Should().Be(SubmissionStatus.Done);
        after.Verdict.Should().Be("Accepted");
        after.Message.Should().BeNull();
    }

    [Fact]
    public async Task When_both_Error_and_Verdict_present_last_branch_wins_current_implementation()
    {
        // В текущем коде блоки независимы:
        //   if (Error) { ... Verdict = "Technical error"; }
        //   if (Verdict) { ... Verdict = command.Verdict; }
        // => Итоговое Verdict перезапишется значением из команды.
        var (ctx, conn) = NewCtx(); await using var _ = conn;

        var sub = MakeSubmission(6, status: SubmissionStatus.Running);
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSubmissionStatusHandler(ctx);

        var cmd = new UpdateSubmissionStatusCommand
        {
            SubmissionId = sub.Id,
            Type = "status",
            Error = "timeout",
            Verdict = "Wrong Answer"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var after = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == sub.Id);

        after.Status.Should().Be(SubmissionStatus.Done);
        after.Verdict.Should().Be("Wrong Answer"); // последнее присваивание «побеждает»
        after.Message.Should().BeNull();
    }
}
