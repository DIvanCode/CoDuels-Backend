using System.Text.Json;
using FluentResults;
using Duely.Application.UseCases.Payloads;     
using Duely.Domain.Models;
using System.Runtime.CompilerServices;
namespace Duely.Application.UseCases.Features.Outbox.Relay;

public sealed class OutboxDispatcher(
    IOutboxHandler<TestSolutionPayload> testSolutionHandler,
    IOutboxHandler<RunUserCodePayload> runUserCodeHandler
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
