using Duely.Application.Services.RateLimiting;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.UserCodeRuns;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

internal sealed class DummyRunUserCodeLimiter : IRunUserCodeLimiter
{
    public Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(false);
}

public class RunUserCodeHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;
        var limiter = new DummyRunUserCodeLimiter();

        var handler = new RunUserCodeHandler(ctx, limiter);

        var res = await handler.Handle(new RunUserCodeCommand
        {
            UserId = 999,
            Code = "print(1)",
            Language = "py",
            Input = "test"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task RateLimitExceeded_when_limit_exceeded()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var limiter = new DummyRunUserCodeLimiterExceeded();

        var handler = new RunUserCodeHandler(ctx, limiter);

        var res = await handler.Handle(new RunUserCodeCommand
        {
            UserId = 1,
            Code = "print(1)",
            Language = "py",
            Input = "test"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is RateLimitExceededError);
    }

    [Fact]
    public async Task Success_creates_run_and_outbox_message()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var limiter = new DummyRunUserCodeLimiter();

        var handler = new RunUserCodeHandler(ctx, limiter);

        var res = await handler.Handle(new RunUserCodeCommand
        {
            UserId = 1,
            Code = "print(1)",
            Language = "py",
            Input = "test input"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue($"errors: {string.Join(" | ", res.Errors.Select(e => e.Message))}");

        // Проверяем, что run создался
        var run = await ctx.UserCodeRuns
            .AsNoTracking()
            .Include(r => r.User)
            .SingleAsync(r => r.Id == res.Value.RunId);

        run.Status.Should().Be(UserCodeRunStatus.Queued);
        run.User!.Id.Should().Be(1);
        run.Language.Should().Be("py");
        run.Code.Should().Be("print(1)");
        run.Input.Should().Be("test input");
        run.Output.Should().BeNull();
        run.Error.Should().BeNull();
        run.ExecutionId.Should().BeNull();

        // Проверяем, что Outbox содержит сообщение RunUserCode
        var outboxMsg = await ctx.Outbox.AsNoTracking().SingleAsync();
        outboxMsg.Type.Should().Be(OutboxType.RunUserCode);
        outboxMsg.Status.Should().Be(OutboxStatus.ToDo);
        outboxMsg.Payload.Should().Contain("print(1)");
        outboxMsg.Payload.Should().Contain("py");
        outboxMsg.Payload.Should().Contain("test input");
    }
}

internal sealed class DummyRunUserCodeLimiterExceeded : IRunUserCodeLimiter
{
    public Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(true);
}