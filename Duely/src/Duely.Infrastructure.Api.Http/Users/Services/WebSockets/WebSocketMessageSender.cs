using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Users.Entities;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Api.Http.Users.Services.WebSockets;

public sealed class WebSocketMessageSender(
    IWebSocketConnectionManager connections,
    ILogger<WebSocketMessageSender> logger)
    : IMessageSender
{
    public async Task SendMessage(UserId userId, Message message, CancellationToken cancellationToken)
    {
        var socket = connections.GetConnection(userId);
        if (socket is null)
        {
            return;
        }

        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to send message {MessageType} to user {UserId}: {Error}",
                message.GetType().Name, userId, ex.Message);
        }
    }
}
