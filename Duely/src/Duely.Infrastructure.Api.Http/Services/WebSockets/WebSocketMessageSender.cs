using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public sealed class WebSocketMessageSender(
    IWebSocketConnectionManager connections,
    ILogger<WebSocketMessageSender> logger)
    : IMessageSender
{
    public async Task SendMessage(int userId, Message message, CancellationToken cancellationToken)
    {
        var sockets = connections.GetSockets(userId);
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var sendTasks = new List<Task>();

        foreach (var socket in sockets)
        {
            if (socket.State != WebSocketState.Open)
            {
                continue;
            }

            sendTasks.Add(SendMessageAsync(socket, payload, message, userId, cancellationToken));
        }

        await Task.WhenAll(sendTasks);
    }

    private async Task SendMessageAsync(
        WebSocket socket,
        byte[] payload,
        Message message,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to send message {MessageType} to user {UserId}: {Error}",
                message.GetType().Name, userId, ex.Message);
        }
    }
}
