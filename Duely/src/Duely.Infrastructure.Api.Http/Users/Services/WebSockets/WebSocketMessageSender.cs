using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Duely.Infrastructure.Gateway.Client.Abstracts;

namespace Duely.Infrastructure.Api.Http.Users.Services.WebSockets;

internal sealed class WebSocketMessageSender(IWebSocketConnectionManager connections) : IMessageSender
{
    public async Task SendMessage(int userId, Message message, CancellationToken cancellationToken)
    {
        var socket = connections.GetConnection(userId);
        if (socket is null)
        {
            throw new InvalidOperationException("Отсутствует WebSocket соединение с пользователем");
        }

        if (socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket соединение с пользователем закрыто");
        }

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
    }
}
