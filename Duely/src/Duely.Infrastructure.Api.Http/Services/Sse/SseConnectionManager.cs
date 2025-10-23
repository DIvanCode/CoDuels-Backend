using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Http.Services.Sse;

public interface ISseConnectionManager
{
    void AddConnection(int userId, HttpResponse response);
    void RemoveConnection(int userId);
    HttpResponse? GetConnection(int userId);
    IReadOnlyCollection<int> GetAllActiveUserIds();
}

public sealed class SseConnectionManager : ISseConnectionManager
{
    private readonly ConcurrentDictionary<int, HttpResponse> _connections = new();

    public void AddConnection(int userId, HttpResponse response)
    {
        _connections[userId] = response;
    }

    public void RemoveConnection(int userId)
    {
        _connections.TryRemove(userId, out _);
    }

    public HttpResponse? GetConnection(int userId)
    {
        return _connections.TryGetValue(userId, out var connection) ? connection : null;
    }

    public IReadOnlyCollection<int> GetAllActiveUserIds()
    {
        return _connections.Keys.ToList().AsReadOnly();
    }
}
