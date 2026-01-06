using Duely.Application.Services.Outbox;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using FluentAssertions;
using FluentResults;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class ExecuteOutboxMessageHandlerTests
{
    [Fact]
    public async Task Delegates_to_dispatcher()
    {
        var dispatcher = new Mock<IOutboxDispatcher>();
        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = "{}",
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        dispatcher
            .Setup(d => d.DispatchAsync(message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var handler = new ExecuteOutboxMessageHandler(dispatcher.Object);

        var result = await handler.Handle(
            new ExecuteOutboxMessageCommand(message),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dispatcher.Verify(d => d.DispatchAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnsFail_when_dispatcher_returns_fail()
    {
        var dispatcher = new Mock<IOutboxDispatcher>();
        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = "{}",
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        dispatcher
            .Setup(d => d.DispatchAsync(message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Dispatcher error"));

        var handler = new ExecuteOutboxMessageHandler(dispatcher.Object);

        var result = await handler.Handle(
            new ExecuteOutboxMessageCommand(message),
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Dispatcher error");
    }
}