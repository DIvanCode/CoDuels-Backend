using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Duely.Infrastructure.Api.Http.Users.Services.WebSockets;

public interface IWebSocketConnectionManager
{
    void AddConnection(Guid userId, WebSocket socket);
    void RemoveConnection(Guid userId);
    WebSocket? GetConnection(Guid userId);
}

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();

    public void AddConnection(Guid userId, WebSocket socket)
    {
        _connections[userId] = socket;
    }

    public void RemoveConnection(Guid userId)
    {
        _connections.TryRemove(userId, out _);
    }

    public WebSocket? GetConnection(Guid userId)
    {
        return _connections.TryGetValue(userId, out var connection) ? connection : null;
    }
}
