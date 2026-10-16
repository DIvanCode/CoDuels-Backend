using System.Net.WebSockets;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public interface IWebSocketConnectionManager
{
    WebSocketRegistration AddConnection(int userId, WebSocket socket);
    DateTime? RemoveConnection(Guid connectionId);
    List<WebSocket> GetSockets(int userId);
    bool HasSockets(int userId);
    IReadOnlyCollection<int> GetLocallyConnectedUserIds();
}

public readonly record struct WebSocketRegistration(Guid ConnectionId, DateTime RegisteredAt);

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _users = new();
    private readonly Dictionary<int, List<Guid>> _connections = new();
    private readonly Dictionary<Guid, WebSocket> _sockets = new();

    public WebSocketRegistration AddConnection(int userId, WebSocket socket)
    {
        lock (_lock)
        {
            var connectionId = Guid.NewGuid();
            _sockets[connectionId] = socket;
            _users[connectionId] = userId;

            var connections = _connections.GetValueOrDefault(userId) ?? new List<Guid>();
            connections.Add(connectionId);
            _connections[userId] = connections;

            return new WebSocketRegistration(connectionId, DateTime.UtcNow);
        }
    }

    public DateTime? RemoveConnection(Guid connectionId)
    {
        lock (_lock)
        {
            DateTime? lastDisconnectedAt = null;
            if (_users.Remove(connectionId, out var userId) &&
                _connections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                {
                    _connections.Remove(userId);
                    lastDisconnectedAt = DateTime.UtcNow;
                }
            }

            _sockets.Remove(connectionId);
            return lastDisconnectedAt;
        }
    }

    public List<WebSocket> GetSockets(int userId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                return [];
            }

            var sockets = new List<WebSocket>();
            foreach (var connection in connections)
            {
                if (_sockets.TryGetValue(connection, out var socket))
                {
                    sockets.Add(socket);
                }
            }

            return sockets;
        }
    }

    public bool HasSockets(int userId)
    {
        lock (_lock)
        {
            return _connections.ContainsKey(userId);
        }
    }

    public IReadOnlyCollection<int> GetLocallyConnectedUserIds()
    {
        lock (_lock)
        {
            return _connections.Keys.ToArray();
        }
    }
}
