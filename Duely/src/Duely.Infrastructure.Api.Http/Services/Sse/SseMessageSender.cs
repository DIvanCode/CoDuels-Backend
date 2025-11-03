using Duely.Infrastructure.Gateway.Client.Abstracts;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Duely.Domain.Models.Messages;

namespace Duely.Infrastructure.Api.Http.Services.Sse;

public sealed class SseMessageSender(ISseConnectionManager connections) : IMessageSender
{
    public async Task SendMessage(int userId, Message message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, message.GetType());

        var response = connections.GetConnection(userId);

        if (response is null)
        {
            return;
        } 

        try
        {
            Console.WriteLine($"==== Send message {message.Type} to user {user.Id}: {json} ====");
            await response.WriteAsync($"event: {message.Type}\n", cancellationToken);
            Console.WriteLine("==== Ok sent event type ====");
            await response.WriteAsync($"data: {json}\n\n", cancellationToken);
            Console.WriteLine("==== Ok sent json data ====");
            await response.Body.FlushAsync(cancellationToken);
            Console.WriteLine("==== Ok sent message ====");
        }
        catch
        {
            Console.WriteLine("==== Failed to sent message ====");
            connections.RemoveConnection(userId);
        }
    }
}
