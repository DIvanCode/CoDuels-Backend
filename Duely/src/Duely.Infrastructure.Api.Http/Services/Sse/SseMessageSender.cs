using Duely.Infrastructure.Gateway.Client.Abstracts;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Duely.Domain.Models.Messages;

namespace Duely.Infrastructure.Api.Http.Services.Sse;

public sealed class SseMessageSender(ISseConnectionManager connections) : IMessageSender
{
    public async Task SendMessage(Message message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, message.GetType());

        foreach (var connection in connections.GetAllActiveUserIds())
        {
            var response = connections.GetConnection(connection);
            if (response is null)
            {
                continue;
            }

            try
            {
                await response.WriteAsync($"event: {message.Type}\n", cancellationToken);
                await response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
                connections.RemoveConnection(connection);
            }
        }
    }
}
