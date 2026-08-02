using System.Net.WebSockets;

namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public interface IWebSocketConnectionManager
{
    Guid AddConnection(int userId, WebSocket socket);
    /// <summary>
    /// Removes a connection and returns a cleanup token only when it was the user's last connection.
    /// </summary>
    Guid? RemoveConnection(Guid connectionId);
    List<WebSocket> GetSockets(int userId);
    bool IsDisconnected(int userId, Guid disconnectToken);
    void CompleteDisconnectCleanup(int userId, Guid disconnectToken);
}

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _users = new();
    private readonly Dictionary<int, List<Guid>> _connections = new();
    private readonly Dictionary<int, Guid> _pendingDisconnects = new();
    private readonly Dictionary<Guid, WebSocket> _sockets = new();

    public Guid AddConnection(int userId, WebSocket socket)
    {
        lock (_lock)
        {
            var connectionId = Guid.NewGuid();
            _pendingDisconnects.Remove(userId);
            _sockets[connectionId] = socket;
            _users[connectionId] = userId;

            var connections = _connections.GetValueOrDefault(userId) ?? new List<Guid>();
            connections.Add(connectionId);
            _connections[userId] = connections;

            return connectionId;
        }
    }

    public Guid? RemoveConnection(Guid connectionId)
    {
        lock (_lock)
        {
            if (!_users.Remove(connectionId, out var userId) ||
                !_connections.TryGetValue(userId, out var connections))
            {
                return null;
            }

            _sockets.Remove(connectionId);
            connections.Remove(connectionId);

            if (connections.Count > 0)
            {
                return null;
            }

            _connections.Remove(userId);
            var disconnectToken = Guid.NewGuid();
            _pendingDisconnects[userId] = disconnectToken;
            return disconnectToken;
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

    public bool IsDisconnected(int userId, Guid disconnectToken)
    {
        lock (_lock)
        {
            return !_connections.ContainsKey(userId) &&
                   _pendingDisconnects.TryGetValue(userId, out var currentToken) &&
                   currentToken == disconnectToken;
        }
    }

    public void CompleteDisconnectCleanup(int userId, Guid disconnectToken)
    {
        lock (_lock)
        {
            if (_pendingDisconnects.TryGetValue(userId, out var currentToken) &&
                currentToken == disconnectToken)
            {
                _pendingDisconnects.Remove(userId);
            }
        }
    }
}
