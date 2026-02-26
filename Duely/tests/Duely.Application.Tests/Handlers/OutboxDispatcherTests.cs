using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentAssertions;
using FluentResults;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class OutboxDispatcherTests
{
    [Fact]
    public async Task Dispatches_TestSolution_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runCodeHandler = new Mock<IOutboxHandler<RunCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new TestSolutionPayload
        {
            TaskId = "TASK-1",
            SubmissionId = 100,
            Solution = "print(1)",
            Language = Language.Python
        };

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = payload,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        testSolutionHandler
            .Setup(h => h.HandleAsync(It.IsAny<TestSolutionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            sendMessageHandler.Object,
            testSolutionHandler.Object,
            runCodeHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        testSolutionHandler.Verify(h => h.HandleAsync(
            It.Is<TestSolutionPayload>(p => p.TaskId == "TASK-1" && p.SubmissionId == 100),
            It.IsAny<CancellationToken>()), Times.Once());
        runCodeHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dispatches_RunUserCode_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runCodeHandler = new Mock<IOutboxHandler<RunCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new RunCodePayload
        {
            RunId = 200,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test"
        };

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.RunUserCode,
            Payload = payload,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        runCodeHandler
            .Setup(h => h.HandleAsync(It.IsAny<RunCodePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            sendMessageHandler.Object,
            testSolutionHandler.Object,
            runCodeHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        runCodeHandler.Verify(h => h.HandleAsync(
            It.Is<RunCodePayload>(p => p.RunId == 200),
            It.IsAny<CancellationToken>()), Times.Once());
        testSolutionHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dispatches_SendMessage_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runCodeHandler = new Mock<IOutboxHandler<RunCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new SendMessagePayload
        {
            UserId = 1,
            Message = new DuelStartedMessage
            {
                DuelId = 10
            }
        };

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.SendMessage,
            Payload = payload,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        sendMessageHandler
            .Setup(h => h.HandleAsync(It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            sendMessageHandler.Object,
            testSolutionHandler.Object,
            runCodeHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sendMessageHandler.Verify(h => h.HandleAsync(
            It.Is<SendMessagePayload>(p => MatchesDuelStartedPayload(p)),
            It.IsAny<CancellationToken>()), Times.Once());
        testSolutionHandler.VerifyNoOtherCalls();
        runCodeHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnsFail_when_handler_returns_fail()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runCodeHandler = new Mock<IOutboxHandler<RunCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new TestSolutionPayload
        {
            TaskId = "TASK-1",
            SubmissionId = 100,
            Solution = "print(1)",
            Language = Language.Python
        };

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = payload,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        testSolutionHandler
            .Setup(h => h.HandleAsync(It.IsAny<TestSolutionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Handler error"));

        var dispatcher = new OutboxDispatcher(
            sendMessageHandler.Object,
            testSolutionHandler.Object,
            runCodeHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Handler error");
    }

    [Fact]
    public async Task ReturnsOk_for_unknown_message_type()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runCodeHandler = new Mock<IOutboxHandler<RunCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var message = new OutboxMessage
        {
            Id = 1,
            Type = (OutboxType)999,
            Payload = new TestSolutionPayload
            {
                TaskId = "TASK-1",
                SubmissionId = 1,
                Solution = "print(1)",
                Language = Language.Python
            },
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        var dispatcher = new OutboxDispatcher(
            sendMessageHandler.Object,
            testSolutionHandler.Object,
            runCodeHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        testSolutionHandler.VerifyNoOtherCalls();
        runCodeHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }

    private static bool MatchesDuelStartedPayload(SendMessagePayload payload)
    {
        return payload.UserId == 1
            && payload.Message is DuelStartedMessage message
            && message.DuelId == 10;
    }
}
