using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentResults;

namespace Duely.Application.Services.Outbox.Relay;

public sealed class OutboxDispatcher(
    IOutboxHandler<SendMessagePayload> sendMessageHandler,
    IOutboxHandler<TestSolutionPayload> testSolutionHandler,
    IOutboxHandler<RunCodePayload> runUserCodeHandler) : IOutboxDispatcher
{
    public Task<Result> DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        return message.Type switch
        {
            OutboxType.SendMessage => sendMessageHandler.HandleAsync((SendMessagePayload)message.Payload, cancellationToken),
            OutboxType.TestSolution => testSolutionHandler.HandleAsync((TestSolutionPayload)message.Payload, cancellationToken),
            OutboxType.RunUserCode => runUserCodeHandler.HandleAsync((RunCodePayload)message.Payload, cancellationToken),
            _ => Task.FromResult(Result.Ok())
        };
    }
}
