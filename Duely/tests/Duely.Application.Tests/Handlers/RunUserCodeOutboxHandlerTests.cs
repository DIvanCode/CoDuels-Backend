using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Outbox.Handlers;
using Duely.Application.UseCases.Payloads;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class RunUserCodeOutboxHandlerTests : ContextBasedTest
{
    [Fact]
    public void Type_ReturnsRunUserCode()
    {
        var ctx = Context;
        var client = new Mock<IExeshClient>();
        var handler = new RunUserCodeOutboxHandler(client.Object, ctx);

        handler.Type.Should().Be(OutboxType.RunUserCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFail_when_run_not_found()
    {
        var ctx = Context;
        var client = new Mock<IExeshClient>();
        var handler = new RunUserCodeOutboxHandler(client.Object, ctx);

        var payload = new RunUserCodePayload(999, "print(1)", "py", "test");

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("999"));
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_SetsExecutionId_when_successful()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = new UserCodeRun
        {
            Id = 100,
            User = u1,
            Code = "print(1)",
            Language = "py",
            Input = "test",
            Status = UserCodeRunStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var client = new Mock<IExeshClient>();
        var execResult = new ExecuteResponse("exec-123");
        client
            .Setup(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(execResult));

        var handler = new RunUserCodeOutboxHandler(client.Object, ctx);

        var payload = new RunUserCodePayload(100, "print(1)", "py", "test");

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        
        var updatedRun = await ctx.UserCodeRuns.SingleAsync(r => r.Id == 100);
        updatedRun.ExecutionId.Should().Be("exec-123");
        
        client.Verify(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFail_when_execution_fails()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = new UserCodeRun
        {
            Id = 100,
            User = u1,
            Code = "print(1)",
            Language = "py",
            Input = "test",
            Status = UserCodeRunStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        ctx.UserCodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var client = new Mock<IExeshClient>();
        client
            .Setup(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Execution error"));

        var handler = new RunUserCodeOutboxHandler(client.Object, ctx);

        var payload = new RunUserCodePayload(100, "print(1)", "py", "test");

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Execution error");
        
        var updatedRun = await ctx.UserCodeRuns.SingleAsync(r => r.Id == 100);
        updatedRun.ExecutionId.Should().BeNull(); // не должно быть установлено при ошибке
    }
}

