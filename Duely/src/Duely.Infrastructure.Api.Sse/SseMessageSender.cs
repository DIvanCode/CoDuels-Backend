using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts.Messages;
using Duely.Application.Configuration;
using System.Text.Json;
using Microsoft.AspNetCore.Http;


namespace Duely.Infrastructure.Api.Sse;

public class SseMessageSender : IMessageSender
{
    private readonly SseConnectionManager _connections;

    public SseMessageSender(SseConnectionManager connections) => _connections = connections;

    public async Task SendMessage(Message message, CancellationToken cancellationToken = default)
    {
        string json = message switch
        {
            DuelStartedMessage duelStarted => JsonSerializer.Serialize(new { duel_id = duelStarted.DuelId }),
            DuelFinishedMessage duelFinished => JsonSerializer.Serialize(new { duel_id = duelFinished.DuelId, winner = duelFinished.Winner }),
            _ => JsonSerializer.Serialize(message)
        };


        var eventName = ConvertMessageTypeToEventName(message.Type);
        if (eventName is null) return;
        
        foreach (var connection in _connections.GetAllActiveUserIds())
        {
            var response = _connections.GetConnection(connection);
            if (response is null) continue;
            
            try 
            {
                await response.WriteAsync($"event: {eventName}\n", cancellationToken);
                await response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
                _connections.RemoveConnection(connection);
            }
        }
        
    }

    private static string ConvertMessageTypeToEventName(MessageType type) 
    {
        return type switch
        {
            MessageType.DuelStarted => "duel_started",
            MessageType.DuelFinished => "duel_finished",
            _ => null
        };
    }
}