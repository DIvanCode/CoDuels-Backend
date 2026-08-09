using System.Net.WebSockets;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public interface IWebSocketConnectionManager
{
    Guid AddConnection(int userId, WebSocket socket);
    void RemoveConnection(Guid connectionId);
    List<WebSocket> GetSockets(int userId);
    bool HasSockets(int userId);
    IReadOnlyCollection<int> GetConnectedUserIds();
}

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _users = new();
    private readonly Dictionary<int, List<Guid>> _connections = new();
    private readonly Dictionary<Guid, WebSocket> _sockets = new();

    public Guid AddConnection(int userId, WebSocket socket)
    {
        lock (_lock)
        {
            var connectionId = Guid.NewGuid();
            _sockets[connectionId] = socket;
            _users[connectionId] = userId;

            var connections = _connections.GetValueOrDefault(userId) ?? new List<Guid>();
            connections.Add(connectionId);
            _connections[userId] = connections;

            return connectionId;
        }
    }

    public void RemoveConnection(Guid connectionId)
    {
        lock (_lock)
        {
            if (_users.Remove(connectionId, out var userId) &&
                _connections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                {
                    _connections.Remove(userId);
                }
            }

            _sockets.Remove(connectionId);
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

    public IReadOnlyCollection<int> GetConnectedUserIds()
    {
        lock (_lock)
        {
            return _connections.Keys.ToArray();
        }
    }
}
