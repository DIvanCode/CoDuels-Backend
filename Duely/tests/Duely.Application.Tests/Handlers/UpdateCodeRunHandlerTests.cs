using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.CodeRuns;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class UpdateCodeRunHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_run_absent()
    {
        var ctx = Context;

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "non-existent-id",
            Type = "start"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Updates_to_running_on_start_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "start"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.CodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Updates_to_running_on_compile_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "compile"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.CodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Updates_to_running_on_run_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.CodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Sends_status_message_when_status_unchanged()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Finishes_with_error_when_error_present()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Error = "Compilation error"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.CodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Error.Should().Be("Compilation error");
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Done);
        outboxMessage.Error.Should().Be("Compilation error");
    }

    [Fact]
    public async Task Finishes_with_output_when_run_status_ok()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Status = "OK",
            Output = "Hello, World!"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.CodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Output.Should().Be("Hello, World!");
        r.Error.Should().BeNull();
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Done);
        outboxMessage.Error.Should().BeNull();
    }

    [Fact]
    public async Task Finishes_with_error_when_run_status_not_ok()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Status = "TLE",
            Error = "TLE"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.CodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Error.Should().Be("TLE");
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Done);
        outboxMessage.Error.Should().Be("TLE");
    }

    [Fact]
    public async Task Finishes_on_finish_type_when_no_error()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "finish"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.CodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        var outboxMessage = await GetSingleStatusMessageAsync(ctx);
        outboxMessage.RunId.Should().Be(100);
        outboxMessage.Status.Should().Be(UserCodeRunStatus.Done);
    }

    [Fact]
    public async Task Returns_ok_when_already_done()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Done);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateCodeRunHandler(ctx);
        var res = await handler.Handle(new UpdateCodeRunCommand
        {
            ExecutionId = "exec-id-1",
            Type = "start"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.CodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Done);
        (await ctx.OutboxMessages.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    private static CodeRun MakeUserCodeRun(int id, User user, string executionId, UserCodeRunStatus status)
    {
        return new CodeRun
        {
            Id = id,
            User = user,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test",
            Status = status,
            Output = null,
            Error = null,
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task<CodeRunStatusUpdatedMessage> GetSingleStatusMessageAsync(Context context)
    {
        var outboxMessages = await context.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();
        outboxMessages.Should().ContainSingle();
        var payload = (SendMessagePayload)outboxMessages[0].Payload;
        payload.Message.Should().BeOfType<CodeRunStatusUpdatedMessage>();
        return (CodeRunStatusUpdatedMessage)payload.Message;
    }
}
