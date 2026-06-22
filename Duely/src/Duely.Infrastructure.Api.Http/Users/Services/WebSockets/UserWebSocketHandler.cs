using System.Net.WebSockets;
using Duely.Application.Handlers.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Users.Services.WebSockets;

public interface IUserWebSocketHandler
{
    Task<IActionResult> HandleConnectionAsync(
        HttpContext httpContext,
        UserDto userDto, 
        CancellationToken cancellationToken);
}

public sealed class UserWebSocketHandler(
    IWebSocketConnectionManager webSocketConnections,
    IOptions<WebSocketConnectionOptions> webSocketOptions,
    ILogger<UserWebSocketHandler> logger) : IUserWebSocketHandler
{
    public async Task<IActionResult> HandleConnectionAsync(
        HttpContext httpContext,
        UserDto userDto,
        CancellationToken cancellationToken)
    {
        var closeTimeout = TimeSpan.FromMilliseconds(webSocketOptions.Value.CloseTimeoutMs);
        
        // TODO: не нравится что отключение вебсокета влияет на бизнес логику. по сути это просто канал сообщений
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            return new BadRequestObjectResult("Некорректный запрос на установление WebSocket соединения.");
        }

        logger.LogInformation("User {Nickname} connected to WebSocket", userDto.Nickname);

        using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
        
        var existingConnection = webSocketConnections.GetConnection(userDto.Id);
        if (existingConnection is not null)
        {
            webSocketConnections.RemoveConnection(userDto.Id);
            await CloseConnectionAsync(existingConnection, "новое соединение", closeTimeout);
        }

        webSocketConnections.AddConnection(userDto.Id, webSocket);

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

                // if (result.MessageType != WebSocketMessageType.Text)
                // {
                //     continue;
                // }
                // 
                // var message = await ReadTextMessageAsync(webSocket, buffer, result, cancellationToken);
                // if (string.IsNullOrWhiteSpace(message))
                // {
                //     continue;
                // }
                // 
                // await HandleEventAsync(userDto.Id, message, cancellationToken);
            }
        }
        finally
        {
            webSocketConnections.RemoveConnection(userDto.Id);
            await CloseConnectionAsync(webSocket, "закрытие соединения", closeTimeout);

            // using (var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            // {
            //     var cleanupToken = cleanupTokenSource.Token;
            //
            //     var cancelResult = await mediator.Send(new CancelPendingDuelsCommand
            //     {
            //         UserId = userDto.Id
            //     }, cleanupToken);
            //     if (cancelResult.IsFailed)
            //     {
            //         logger.LogWarning(
            //             "WebSocket cleanup failed to cancel search for user {UserId}: {Error}",
            //             userDto.Id, string.Join(", ", cancelResult.Errors));
            //     }
            //
            // }
            
            logger.LogInformation("User {Nickname} disconnected from WebSocket", userDto.Nickname);
        }

        return new EmptyResult();
    }

    private static async Task CloseConnectionAsync(WebSocket socket, string purpose, TimeSpan closeTimeout)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var closeTokenSource = new CancellationTokenSource(closeTimeout);
            
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, purpose, closeTokenSource.Token);
                return;
            }
            catch
            {
                // ignored
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

    // private static async Task<string> ReadTextMessageAsync(
    //     WebSocket webSocket,
    //     byte[] buffer,
    //     WebSocketReceiveResult result,
    //     CancellationToken cancellationToken)
    // {
    //     using var stream = new MemoryStream();
    //     stream.Write(buffer, 0, result.Count);
    //
    //     while (!result.EndOfMessage)
    //     {
    //         result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
    //         stream.Write(buffer, 0, result.Count);
    //     }
    //
    //     return Encoding.UTF8.GetString(stream.ToArray());
    // }

    // private async Task HandleEventAsync(Guid userId, string message, CancellationToken cancellationToken)
    // {
    //     Event? request;
    //     try
    //     {
    //         request = JsonSerializer.Deserialize<Event>(message);
    //     }
    //     catch (JsonException)
    //     {
    //         logger.LogWarning("WebSocket message ignored: invalid json");
    //         return;
    //     }
    //
    //     if (request is null)
    //     {
    //         logger.LogWarning("WebSocket message ignored: empty payload");
    //         return;
    //     }
    //
    //     switch (request)
    //     {
    //         case SolutionUpdatedEvent solutionUpdated:
    //             await HandleSolutionUpdatedEventAsync(userId, solutionUpdated, cancellationToken);
    //             return;
    //         default:
    //             logger.LogWarning("WebSocket message ignored: unknown type");
    //             return;
    //     }
    // }

    // private async Task HandleSolutionUpdatedEventAsync(
    //     Guid userId,
    //     SolutionUpdatedEvent request,
    //     CancellationToken cancellationToken)
    // {
    //     if (string.IsNullOrWhiteSpace(request.TaskKey) || request.TaskKey.Length != 1)
    //     {
    //         logger.LogWarning("WebSocket message ignored: invalid task key");
    //         return;
    //     }
    //
    //     var command = new UpdateDuelTaskSolutionCommand
    //     {
    //         UserId = userId,
    //         DuelId = request.DuelId,
    //         TaskKey = request.TaskKey[0],
    //         Solution = request.Solution,
    //         Language = request.Language
    //     };
    //
    //     var result = await mediator.Send(command, cancellationToken);
    //     if (result.IsFailed)
    //     {
    //         logger.LogWarning(
    //             "Failed to update duel task solution for user {UserId}: {Error}",
    //             userId, string.Join(", ", result.Errors));
    //     }
    // }
}
