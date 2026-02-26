using Duely.Application.Services.Outbox.Handlers;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class RunCodeOutboxHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task HandleAsync_ReturnsFail_when_run_not_found()
    {
        var ctx = Context;
        var client = new Mock<IExeshClient>();
        var handler = new RunCodeOutboxHandler(client.Object, ctx);

        var payload = new RunCodePayload
        {
            RunId = 999,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test"
        };

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
        var run = new CodeRun
        {
            Id = 100,
            User = u1,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test",
            Status = UserCodeRunStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var client = new Mock<IExeshClient>();
        var execResult = new ExecuteResponse("exec-123");
        client
            .Setup(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(execResult));

        var handler = new RunCodeOutboxHandler(client.Object, ctx);

        var payload = new RunCodePayload
        {
            RunId = 100,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test"
        };

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        
        var updatedRun = await ctx.CodeRuns.SingleAsync(r => r.Id == 100);
        updatedRun.ExecutionId.Should().Be("exec-123");
        
        client.Verify(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFail_when_execution_fails()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = new CodeRun
        {
            Id = 100,
            User = u1,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test",
            Status = UserCodeRunStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var client = new Mock<IExeshClient>();
        client
            .Setup(c => c.ExecuteAsync(It.IsAny<ExeshStep[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Execution error"));

        var handler = new RunCodeOutboxHandler(client.Object, ctx);

        var payload = new RunCodePayload
        {
            RunId = 100,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test"
        };

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Execution error");
        
        var updatedRun = await ctx.CodeRuns.SingleAsync(r => r.Id == 100);
        updatedRun.ExecutionId.Should().BeNull(); // не должно быть установлено при ошибке
    }
}
