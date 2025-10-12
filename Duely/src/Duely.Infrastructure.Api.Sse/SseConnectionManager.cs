using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Sse;

public class SseConnectionManager
{
    private readonly ConcurrentDictionary<int, HttpResponse> _connections = new();

    public void AddConnection(int duelId, HttpResponse response) => _connections[duelId] = response;

    public void RemoveConnection(int duelId) => _connections.TryRemove(duelId, out _);

    public HttpResponse GetConnection(int duelId) => _connections.TryGetValue(duelId, out var connection) ? connection : null;

    public IReadOnlyCollection<int> GetAllActiveUserIds() => _connections.Keys;

}