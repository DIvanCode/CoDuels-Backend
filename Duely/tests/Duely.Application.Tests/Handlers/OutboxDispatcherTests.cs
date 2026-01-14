using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using FluentAssertions;
using FluentResults;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class OutboxDispatcherTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Dispatches_TestSolution_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new TestSolutionPayload("TASK-1", 100, "print(1)", "py");
        var payloadJson = JsonSerializer.Serialize(payload, Json);

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = payloadJson,
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        testSolutionHandler
            .Setup(h => h.HandleAsync(It.IsAny<TestSolutionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        testSolutionHandler.Verify(h => h.HandleAsync(
            It.Is<TestSolutionPayload>(p => p.TaskId == "TASK-1" && p.SubmissionId == 100),
            It.IsAny<CancellationToken>()), Times.Once);
        runUserCodeHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dispatches_RunUserCode_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new RunUserCodePayload(200, "print(1)", "py", "test");
        var payloadJson = JsonSerializer.Serialize(payload, Json);

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.RunUserCode,
            Payload = payloadJson,
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        runUserCodeHandler
            .Setup(h => h.HandleAsync(It.IsAny<RunUserCodePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        runUserCodeHandler.Verify(h => h.HandleAsync(
            It.Is<RunUserCodePayload>(p => p.RunId == 200),
            It.IsAny<CancellationToken>()), Times.Once);
        testSolutionHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dispatches_SendMessage_message()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new SendMessagePayload(1, MessageType.DuelStarted, 10);
        var payloadJson = JsonSerializer.Serialize(payload, Json);

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.SendMessage,
            Payload = payloadJson,
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        sendMessageHandler
            .Setup(h => h.HandleAsync(It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sendMessageHandler.Verify(h => h.HandleAsync(
            It.Is<SendMessagePayload>(p => p.UserId == 1 && p.DuelId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        testSolutionHandler.VerifyNoOtherCalls();
        runUserCodeHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Throws_when_payload_deserialization_fails()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = "invalid json",
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var act = async () => await dispatcher.DispatchAsync(message, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
        testSolutionHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnsFail_when_handler_returns_fail()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var payload = new TestSolutionPayload("TASK-1", 100, "print(1)", "py");
        var payloadJson = JsonSerializer.Serialize(payload, Json);

        var message = new OutboxMessage
        {
            Id = 1,
            Type = OutboxType.TestSolution,
            Payload = payloadJson,
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        testSolutionHandler
            .Setup(h => h.HandleAsync(It.IsAny<TestSolutionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Handler error"));

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Handler error");
    }

    [Fact]
    public async Task ReturnsOk_for_unknown_message_type()
    {
        var testSolutionHandler = new Mock<IOutboxHandler<TestSolutionPayload>>();
        var runUserCodeHandler = new Mock<IOutboxHandler<RunUserCodePayload>>();
        var sendMessageHandler = new Mock<IOutboxHandler<SendMessagePayload>>();

        var message = new OutboxMessage
        {
            Id = 1,
            Type = (OutboxType)999, // неизвестный тип
            Payload = "{}",
            Status = OutboxStatus.ToDo,
            RetryUntil = DateTime.UtcNow.AddMinutes(5)
        };

        var dispatcher = new OutboxDispatcher(
            testSolutionHandler.Object,
            runUserCodeHandler.Object,
            sendMessageHandler.Object);

        var result = await dispatcher.DispatchAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        testSolutionHandler.VerifyNoOtherCalls();
        runUserCodeHandler.VerifyNoOtherCalls();
        sendMessageHandler.VerifyNoOtherCalls();
    }
}