using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.UserCodeRuns;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class UpdateUserCodeRunStatusHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_run_absent()
    {
        var ctx = Context;

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "non-existent-id",
            Type = "start"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is Duely.Application.UseCases.Errors.EntityNotFoundError);
    }

    [Fact]
    public async Task Updates_to_running_on_start_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "start"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.UserCodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Updates_to_running_on_compile_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "compile"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.UserCodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Updates_to_running_on_run_type()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Queued);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.UserCodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Running);
    }

    [Fact]
    public async Task Finishes_with_error_when_error_present()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Error = "Compilation error"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.UserCodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Error.Should().Be("Compilation error");
    }

    [Fact]
    public async Task Finishes_with_output_when_run_status_ok()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Status = "OK",
            Output = "Hello, World!"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.UserCodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Output.Should().Be("Hello, World!");
        r.Error.Should().BeNull();
    }

    [Fact]
    public async Task Finishes_with_error_when_run_status_not_ok()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "run",
            Status = "TLE"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.UserCodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
        r.Error.Should().Be("TLE");
    }

    [Fact]
    public async Task Finishes_on_finish_type_when_no_error()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Running);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "finish"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var r = await ctx.UserCodeRuns.AsNoTracking().SingleAsync(x => x.Id == 100);
        r.Status.Should().Be(UserCodeRunStatus.Done);
    }

    [Fact]
    public async Task Returns_ok_when_already_done()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeUserCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Done);
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCodeRunStatusHandler(ctx);
        var res = await handler.Handle(new UpdateUserCodeRunStatusCommand
        {
            ExecutionId = "exec-id-1",
            Type = "start"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.UserCodeRuns.AsNoTracking().SingleAsync(r => r.Id == 100)).Status.Should().Be(UserCodeRunStatus.Done);
    }

    private static UserCodeRun MakeUserCodeRun(int id, User user, string executionId, UserCodeRunStatus status)
    {
        return new UserCodeRun
        {
            Id = id,
            User = user,
            Code = "print(1)",
            Language = "py",
            Input = "test",
            Status = status,
            Output = null,
            Error = null,
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

