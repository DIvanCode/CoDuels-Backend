using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentResults;

namespace Duely.Application.Services.Outbox.Handlers;

public sealed class SendMessageOutboxHandler(IMessageSender sender) : IOutboxHandler<SendMessagePayload>
{
    public async Task<Result> HandleAsync(SendMessagePayload payload, CancellationToken cancellationToken)
    {
        await sender.SendMessage(payload.UserId, payload.Message, cancellationToken);

        return Result.Ok();
    }
}
