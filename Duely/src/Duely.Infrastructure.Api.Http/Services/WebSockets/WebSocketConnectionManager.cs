using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public interface IWebSocketConnectionManager
{
    void AddConnection(int userId, WebSocket socket);
    void RemoveConnection(int userId);
    WebSocket? GetConnection(int userId);
}

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<int, WebSocket> _connections = new();

    public void AddConnection(int userId, WebSocket socket)
    {
        _connections[userId] = socket;
    }

    public void RemoveConnection(int userId)
    {
        _connections.TryRemove(userId, out _);
    }

    public WebSocket? GetConnection(int userId)
    {
        return _connections.TryGetValue(userId, out var connection) ? connection : null;
    }
}
