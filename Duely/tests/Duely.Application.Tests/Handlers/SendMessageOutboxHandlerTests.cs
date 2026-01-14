using Duely.Application.Services.Outbox.Handlers;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentAssertions;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class SendMessageOutboxHandlerTests
{
    [Fact]
    public void Type_ReturnsSendMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        handler.Type.Should().Be(OutboxType.SendMessage);
    }

    [Fact]
    public async Task HandleAsync_SendsDuelStartedMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload(1, MessageType.DuelStarted, 10);

        sender
            .Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(s => s.SendMessage(
            1,
            It.Is<DuelStartedMessage>(m => m.DuelId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SendsDuelFinishedMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload(2, MessageType.DuelFinished, 20);

        sender
            .Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(s => s.SendMessage(
            2,
            It.Is<DuelFinishedMessage>(m => m.DuelId == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SendsDuelCanceledMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload(2, MessageType.DuelCanceled, 0, "u1");

        sender
            .Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(s => s.SendMessage(
            2,
            It.Is<DuelCanceledMessage>(m => m.OpponentNickname == "u1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ThrowsForUnknownMessageType()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload(1, (MessageType)999, 10); // неизвестный тип

        var act = async () => await handler.HandleAsync(payload, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        sender.VerifyNoOtherCalls();
    }
}
