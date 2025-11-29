using Duely.Infrastructure.Gateway.Client.Abstracts;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Duely.Domain.Models.Messages;

namespace Duely.Infrastructure.Api.Http.Services.Sse;

public sealed class SseMessageSender(ISseConnectionManager connections) : IMessageSender
{
    public async Task SendMessage(int userId, Message message, CancellationToken cancellationToken)
    {
        var response = connections.GetConnection(userId);
        if (response is null)
        {
            return;
        } 

        try
        {
            Console.WriteLine($"Sending message {message.Type} to user {userId}");
            
            var json = JsonSerializer.Serialize(message, message.GetType());
            
            await response.WriteAsync($"event: {message.Type}\n", cancellationToken);
            await response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
            
            Console.WriteLine($"Ok sent message {message.Type} to user {userId}");
        }
        catch
        {
            Console.WriteLine($"Failed to send message {message.Type} to user {userId}");
        }
    }
}
