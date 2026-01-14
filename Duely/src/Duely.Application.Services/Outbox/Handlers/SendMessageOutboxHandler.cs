using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentResults;

namespace Duely.Application.Services.Outbox.Handlers;

public sealed class SendMessageOutboxHandler(IMessageSender sender)
    : IOutboxHandler<SendMessagePayload>
{
    public OutboxType Type => OutboxType.SendMessage;

    public async Task<Result> HandleAsync(SendMessagePayload payload, CancellationToken cancellationToken)
    {
        Message message = payload.Type switch
        {
            MessageType.DuelStarted => new DuelStartedMessage { DuelId = payload.DuelId },
            MessageType.DuelFinished => new DuelFinishedMessage { DuelId = payload.DuelId },
            MessageType.DuelChanged => new DuelChangedMessage { DuelId = payload.DuelId },
            MessageType.DuelCanceled => new DuelCanceledMessage
            {
                OpponentNickname = payload.OpponentNickname
                    ?? throw new ArgumentException("Opponent nickname is required.", nameof(payload))
            },
            _ => throw new ArgumentOutOfRangeException(nameof(payload.Type), payload.Type, null)
        };

        await sender.SendMessage(payload.UserId, message, cancellationToken);

        return Result.Ok();
    }
}
