using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models;
using FluentResults;

namespace Duely.Application.Services.Outbox.Relay;

public sealed class OutboxDispatcher(
    IOutboxHandler<TestSolutionPayload> testSolutionHandler,
    IOutboxHandler<RunUserCodePayload> runUserCodeHandler,
    IOutboxHandler<SendMessagePayload> sendMessageHandler
    ) : IOutboxDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<Result> DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type == OutboxType.TestSolution)
        {
            return Handle(testSolutionHandler, message.Payload, cancellationToken);
        }
        if (message.Type == OutboxType.RunUserCode)
        {
            return Handle(runUserCodeHandler, message.Payload, cancellationToken);
        }
        if (message.Type == OutboxType.SendMessage)
        {
            return Handle(sendMessageHandler, message.Payload, cancellationToken);
        }
    
        return Task.FromResult(Result.Ok());
    }

    private static Task<Result> Handle<TPayload>(IOutboxHandler<TPayload> handler, string payload, CancellationToken cancellationToken)  where TPayload : IOutboxPayload     
    {
        var parsed = JsonSerializer.Deserialize<TPayload>(payload, Json);
        if (parsed is null)
            return Task.FromResult(Result.Fail($"Invalid {typeof(TPayload).Name} payload"));
        return handler.HandleAsync(parsed, cancellationToken);
    }
}
