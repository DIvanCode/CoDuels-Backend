using Duely.Infrastructure.Gateway.Client.Abstracts;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Duely.Domain.Models.Messages;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Api.Http.Services.Sse;

public sealed class SseMessageSender(ISseConnectionManager connections, ILogger<SseMessageSender> logger) : IMessageSender
{
    public async Task SendMessage(int userId, Message message, CancellationToken cancellationToken)
    {
        var response = connections.GetConnection(userId);
        if (response is null)
        {
            logger.LogDebug("SSE send skipped: no connection. UserId = {UserId}, MessageType = {MessageType}", userId, message.Type);

            return;
        }

        try
        {
            logger.LogInformation("Sending message {MessageType} to user {UserId}", message.Type, userId);

            var json = JsonSerializer.Serialize(message, message.GetType());

            await response.WriteAsync($"event: {message.Type}\n", cancellationToken);
            await response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);

            logger.LogInformation("Ok sent message {MessageType} to user {UserId}", message.Type, userId);
        }
        catch
        {
            logger.LogWarning("Failed to send message {MessageType} to user {UserId}", message.Type, userId);
        }
    }
}
