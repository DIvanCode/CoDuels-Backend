using Duely.Application.Services.Errors;
using Duely.Application.Services.RateLimiting;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.CodeRuns;
using Duely.Domain.Models;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

internal sealed class DummyRunUserCodeLimiter : IRunUserCodeLimiter
{
    public Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(false);
}

public class CreateCodeRunHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;
        var limiter = new DummyRunUserCodeLimiter();

        var handler = new CreateCodeRunHandler(ctx, limiter);

        var res = await handler.Handle(new CreateCodeRunCommand
        {
            UserId = 999,
            Code = "print(1)",
            Language = Language.Python,
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

        var handler = new CreateCodeRunHandler(ctx, limiter);

        var res = await handler.Handle(new CreateCodeRunCommand
        {
            UserId = 1,
            Code = "print(1)",
            Language = Language.Python,
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

        var handler = new CreateCodeRunHandler(ctx, limiter);

        var res = await handler.Handle(new CreateCodeRunCommand
        {
            UserId = 1,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test input"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue($"errors: {string.Join(" | ", res.Errors.Select(e => e.Message))}");

        // Проверяем, что run создался
        var run = await ctx.CodeRuns
            .AsNoTracking()
            .Include(r => r.User)
            .SingleAsync(r => r.Id == res.Value.Id);

        run.Status.Should().Be(UserCodeRunStatus.Queued);
        run.User!.Id.Should().Be(1);
        run.Language.Should().Be(Language.Python);
        run.Code.Should().Be("print(1)");
        run.Input.Should().Be("test input");
        run.Output.Should().BeNull();
        run.Error.Should().BeNull();
        run.ExecutionId.Should().BeNull();

        // Проверяем, что OutboxMessages содержит сообщение RunUserCode
        var outboxMsg = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMsg.Type.Should().Be(OutboxType.RunUserCode);
        outboxMsg.Status.Should().Be(OutboxStatus.ToDo);
        outboxMsg.Payload.Should().BeOfType<RunCodePayload>();
        var payload = (RunCodePayload)outboxMsg.Payload;
        payload.RunId.Should().Be(run.Id);
        payload.Code.Should().Be("print(1)");
        payload.Language.Should().Be(Language.Python);
        payload.Input.Should().Be("test input");
    }
}

internal sealed class DummyRunUserCodeLimiterExceeded : IRunUserCodeLimiter
{
    public Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
