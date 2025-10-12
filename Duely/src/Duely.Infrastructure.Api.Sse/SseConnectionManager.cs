using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Sse;

public class SseConnectionManager
{
    private readonly ConcurrentDictionary<int, HttpResponse> _connections = new();

    public void AddConnection(int userId, HttpResponse response) => _connections[userId] = response;

    public void RemoveConnection(int userId) => _connections.TryRemove(userId, out _);

    public HttpResponse GetConnection(int userId) => _connections.TryGetValue(userId, out var connection) ? connection : null;

    public IReadOnlyCollection<int> GetAllActiveUserIds() => _connections.Keys;

}