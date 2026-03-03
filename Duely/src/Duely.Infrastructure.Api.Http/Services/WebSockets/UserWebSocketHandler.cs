using Duely.Application.UseCases.Features.Duels;
using Duely.Infrastructure.Api.Http.Events;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public interface IUserWebSocketHandler
{
    Task<IActionResult> HandleConnectionAsync(HttpContext httpContext, int userId, CancellationToken cancellationToken);
}

public sealed class UserWebSocketHandler(
    IMediator mediator,
    IWebSocketConnectionManager webSocketConnections,
    IOptions<WebSocketConnectionOptions> webSocketOptions,
    ILogger<UserWebSocketHandler> logger) : IUserWebSocketHandler
{
    public async Task<IActionResult> HandleConnectionAsync(
        HttpContext httpContext,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            return new BadRequestObjectResult("WebSocket request expected.");
        }

        logger.LogInformation("WebSocket connected user {UserId}", userId);

        using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
        var existingConnection = webSocketConnections.GetConnection(userId);
        if (existingConnection is not null)
        {
            webSocketConnections.RemoveConnection(userId);
            await CloseExistingConnectionAsync(existingConnection, webSocketOptions.Value.CloseTimeoutMs);
        }

        webSocketConnections.AddConnection(userId, webSocket);

        try
        {
            var buffer = new byte[4 * 1024];

            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var message = await ReadTextMessageAsync(webSocket, buffer, result, cancellationToken);
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                await HandleEventAsync(userId, message, cancellationToken);
            }
        }
        finally
        {
            using var closeTokenSource = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(webSocketOptions.Value.CloseTimeoutMs));
            var closeToken = closeTokenSource.Token;

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeToken);
            }

            webSocketConnections.RemoveConnection(userId);

            using (var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var cleanupToken = cleanupTokenSource.Token;

                var cancelResult = await mediator.Send(new CancelPendingDuelsCommand
                {
                    UserId = userId
                }, cleanupToken);
                if (cancelResult.IsFailed)
                {
                    logger.LogWarning(
                        "WebSocket cleanup failed to cancel search for user {UserId}: {Error}",
                        userId, string.Join(", ", cancelResult.Errors));
                }

            }

            logger.LogInformation("WebSocket disconnected user {UserId}", userId);
        }

        return new EmptyResult();
    }

    private static async Task CloseExistingConnectionAsync(WebSocket socket, int closeTimeoutMs)
    {
        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            using var closeTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(closeTimeoutMs));
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replaced by new connection", closeTokenSource.Token);
                return;
            }
            catch
            {
                // fall through to abort
            }
        }

        try
        {
            socket.Abort();
        }
        catch
        {
            // ignored
        }
    }

    private static async Task<string> ReadTextMessageAsync(
        WebSocket webSocket,
        byte[] buffer,
        WebSocketReceiveResult result,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        stream.Write(buffer, 0, result.Count);

        while (!result.EndOfMessage)
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            stream.Write(buffer, 0, result.Count);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task HandleEventAsync(int userId, string message, CancellationToken cancellationToken)
    {
        Event? request;
        try
        {
            request = JsonSerializer.Deserialize<Event>(message);
        }
        catch (JsonException)
        {
            logger.LogWarning("WebSocket message ignored: invalid json");
            return;
        }

        if (request is null)
        {
            logger.LogWarning("WebSocket message ignored: empty payload");
            return;
        }

        switch (request)
        {
            case SolutionUpdatedEvent solutionUpdated:
                await HandleSolutionUpdatedEventAsync(userId, solutionUpdated, cancellationToken);
                return;
            default:
                logger.LogWarning("WebSocket message ignored: unknown type");
                return;
        }
    }

    private async Task HandleSolutionUpdatedEventAsync(
        int userId,
        SolutionUpdatedEvent request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TaskKey) || request.TaskKey.Length != 1)
        {
            logger.LogWarning("WebSocket message ignored: invalid task key");
            return;
        }

        var command = new UpdateDuelTaskSolutionCommand
        {
            UserId = userId,
            DuelId = request.DuelId,
            TaskKey = request.TaskKey[0],
            Solution = request.Solution,
            Language = request.Language
        };

        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailed)
        {
            logger.LogWarning("Failed to update duel task solution for user {UserId}: {Error}",
                userId, string.Join(", ", result.Errors));
        }
    }

}
