using Duely.Application.Services.Outbox.Handlers;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentAssertions;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class SendMessageOutboxHandlerTests
{
    [Fact]
    public async Task HandleAsync_SendsDuelStartedMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload
        {
            UserId = 1,
            Message = new DuelStartedMessage
            {
                DuelId = 10
            }
        };

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

        var payload = new SendMessagePayload
        {
            UserId = 2,
            Message = new DuelFinishedMessage
            {
                DuelId = 20
            }
        };

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
    public async Task HandleAsync_SendsDuelInvitationCanceledMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload
        {
            UserId = 2,
            Message = new DuelInvitationCanceledMessage
            {
                OpponentNickname = "u1"
            }
        };

        sender
            .Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(s => s.SendMessage(
            2,
            It.Is<DuelInvitationCanceledMessage>(m => m.OpponentNickname == "u1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SendsDuelCodeUpdatedMessage()
    {
        var sender = new Mock<IMessageSender>();
        var handler = new SendMessageOutboxHandler(sender.Object);

        var payload = new SendMessagePayload
        {
            UserId = 3,
            Message = new OpponentSolutionUpdatedMessage
            {
                DuelId = 11,
                TaskKey = "A",
                Solution = "print(1)",
                Language = Language.Python
            }
        };

        sender
            .Setup(s => s.SendMessage(3, It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(s => s.SendMessage(
            3,
            It.Is<OpponentSolutionUpdatedMessage>(m =>
                m.DuelId == 11 &&
                m.TaskKey == "A" &&
                m.Solution == "print(1)" &&
                m.Language == Language.Python),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
