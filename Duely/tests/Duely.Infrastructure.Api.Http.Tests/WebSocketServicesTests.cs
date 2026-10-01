using System.Net.WebSockets;
using System.Text;
using Duely.Application.UseCases.Features.Duels;
using Duely.Infrastructure.Api.Http.Services.WebSockets;
using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Duely.Domain.Models.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class WebSocketServicesTests
{
    [Fact]
    public void GetLocallyConnectedUserIds_returns_each_user_until_their_last_local_connection_is_removed()
    {
        var manager = new WebSocketConnectionManager();
        var firstUserConnection = manager.AddConnection(42, new Mock<WebSocket>().Object);
        var secondUserConnection = manager.AddConnection(42, new Mock<WebSocket>().Object);
        manager.AddConnection(7, new Mock<WebSocket>().Object);

        manager.GetLocallyConnectedUserIds().Should().BeEquivalentTo([42, 7]);

        manager.RemoveConnection(firstUserConnection);
        manager.GetLocallyConnectedUserIds().Should().BeEquivalentTo([42, 7]);

        manager.RemoveConnection(secondUserConnection);
        manager.GetLocallyConnectedUserIds().Should().BeEquivalentTo([7]);
    }

    [Fact]
    public void Connection_managers_keep_presence_isolated_per_process_instance()
    {
        var firstInstance = new WebSocketConnectionManager();
        var secondInstance = new WebSocketConnectionManager();
        firstInstance.AddConnection(42, new Mock<WebSocket>().Object);
        secondInstance.AddConnection(7, new Mock<WebSocket>().Object);

        firstInstance.GetLocallyConnectedUserIds().Should().BeEquivalentTo([42]);
        secondInstance.GetLocallyConnectedUserIds().Should().BeEquivalentTo([7]);
    }

    [Fact]
    public void RemoveConnection_RemovesOnlyTheClosedSocket()
    {
        var manager = new WebSocketConnectionManager();
        var firstSocket = new Mock<WebSocket>().Object;
        var secondSocket = new Mock<WebSocket>().Object;

        var firstConnectionId = manager.AddConnection(42, firstSocket);
        var secondConnectionId = manager.AddConnection(42, secondSocket);

        manager.RemoveConnection(firstConnectionId);
        manager.GetSockets(42).Should().ContainSingle().Which.Should().BeSameAs(secondSocket);

        manager.RemoveConnection(secondConnectionId);

        manager.HasSockets(42).Should().BeFalse();
    }

    [Fact]
    public async Task SendMessage_SendsToEveryOpenSocketAndSkipsClosedSockets()
    {
        var manager = new WebSocketConnectionManager();
        var closedSocket = new Mock<WebSocket>(MockBehavior.Strict);
        closedSocket.SetupGet(socket => socket.State).Returns(WebSocketState.CloseSent);

        var firstOpenSocket = CreateOpenSocket();
        var secondOpenSocket = CreateOpenSocket();
        manager.AddConnection(42, closedSocket.Object);
        manager.AddConnection(42, firstOpenSocket.Object);
        manager.AddConnection(42, secondOpenSocket.Object);

        var logger = new Mock<ILogger<WebSocketMessageSender>>();
        var sender = new WebSocketMessageSender(manager, logger.Object);

        await sender.SendMessage(
            42,
            new DuelStartedMessage { DuelId = 1 },
            CancellationToken.None);

        closedSocket.Verify(
            socket => socket.SendAsync(
                It.IsAny<ArraySegment<byte>>(),
                WebSocketMessageType.Text,
                true,
                It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyMessageSent(firstOpenSocket);
        VerifyMessageSent(secondOpenSocket);
    }

    [Fact]
    public async Task Invalid_solution_update_does_not_disconnect_or_drop_the_next_frame()
    {
        var frames = new Queue<(string Json, WebSocketMessageType Type)>(
        [
            ("""{"type":"SolutionUpdated","duel_id":0,"task_key":"A","language":"Python","solution":"old"}""", WebSocketMessageType.Text),
            ("""{"type":"SolutionUpdated","duel_id":7,"task_key":"A","language":"Python","solution":""}""", WebSocketMessageType.Text),
            (string.Empty, WebSocketMessageType.Close)
        ]);
        var socket = new Mock<WebSocket>();
        socket.SetupGet(value => value.State).Returns(WebSocketState.Open);
        socket.Setup(value => value.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
            .Returns<ArraySegment<byte>, CancellationToken>((buffer, _) =>
            {
                var (json, type) = frames.Dequeue();
                var bytes = Encoding.UTF8.GetBytes(json);
                bytes.CopyTo(buffer.Array!.AsSpan(buffer.Offset));
                return Task.FromResult(new WebSocketReceiveResult(bytes.Length, type, true));
            });
        socket.Setup(value => value.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var feature = new Mock<IHttpWebSocketFeature>();
        feature.SetupGet(value => value.IsWebSocketRequest).Returns(true);
        feature.Setup(value => value.AcceptAsync(It.IsAny<WebSocketAcceptContext>()))
            .ReturnsAsync(socket.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(feature.Object);

        var mediator = new Mock<IMediator>();
        mediator.Setup(value => value.Send(
                It.IsAny<UpdateDuelTaskSolutionCommand>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateDuelTaskSolutionCommand, CancellationToken>((command, _) =>
                command.DuelId == 0
                    ? Task.FromException<Result>(new ValidationException(
                        [new ValidationFailure("DuelId", "Duel id is required.")]))
                    : Task.FromResult(Result.Ok()));

        var connections = new WebSocketConnectionManager();
        connections.AddConnection(42, new Mock<WebSocket>().Object);
        var handler = new UserWebSocketHandler(
            mediator.Object,
            connections,
            Options.Create(new WebSocketConnectionOptions { CloseTimeoutMs = 100 }),
            NullLogger<UserWebSocketHandler>.Instance);

        await handler.HandleConnectionAsync(httpContext, 42, CancellationToken.None);

        mediator.Verify(value => value.Send(
            It.Is<UpdateDuelTaskSolutionCommand>(command => command.DuelId == 7 && command.Solution == string.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<WebSocket> CreateOpenSocket()
    {
        var socket = new Mock<WebSocket>(MockBehavior.Strict);
        socket.SetupGet(value => value.State).Returns(WebSocketState.Open);
        socket
            .Setup(value => value.SendAsync(
                It.IsAny<ArraySegment<byte>>(),
                WebSocketMessageType.Text,
                true,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return socket;
    }

    private static void VerifyMessageSent(Mock<WebSocket> socket)
    {
        socket.Verify(
            value => value.SendAsync(
                It.IsAny<ArraySegment<byte>>(),
                WebSocketMessageType.Text,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
