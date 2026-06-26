using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class SendMessageIntegrationEventHandler(
    IMessageSender messageSender,
    ILogger<SendMessageIntegrationEventHandler> logger)
    : IntegrationEventHandler<SendMessageIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.SendMessage;
    
    public override async Task<Result> Handle(
        SendMessageIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.CreatedAt.Add(integrationEvent.ExpirationTime) < DateTime.UtcNow)
        {
            return new IntegrationEventExpiredError();
        }

        try
        {
            await messageSender.SendMessage(integrationEvent.UserId, integrationEvent.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to send message {MessageType} to user {UserId} due to error: {Error}",
                integrationEvent.Message.GetType().Name, integrationEvent.UserId, ex.Message);
            return new SendMessageError();
        }

        return Result.Ok();
    }
}

internal sealed class SendMessageError() : Error("Не удалось отправить сообщение пользователю.");
