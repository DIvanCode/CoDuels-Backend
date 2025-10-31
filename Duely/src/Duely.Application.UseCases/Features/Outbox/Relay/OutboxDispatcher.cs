using System.Text.Json;
using FluentResults;
using Duely.Application.UseCases.Payloads;     
using DomainOutbox = Duely.Domain.Models.Outbox;
using OutboxType = Duely.Domain.Models.OutboxType;
namespace Duely.Application.UseCases.Features.Outbox.Relay;

public sealed class OutboxDispatcher(IOutboxHandler<TestSolutionPayload> testSolutionHandler) : IOutboxDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<Result> DispatchAsync(DomainOutbox message, CancellationToken cancellationToken)
    {
        if (message.Type == OutboxType.TestSolution)
        {
            return Handle(testSolutionHandler, message.Payload, cancellationToken);
        }
        if (message.Type == OutboxType.SendMessage)
        {
            
        }
    
        return Task.FromResult(Result.Ok());
    }

    private static Task<Result> Handle<T>(IOutboxHandler<T> handler, string payload, CancellationToken cancellationToken)
    {
        var parsed = JsonSerializer.Deserialize<T>(payload, Json);
        if (parsed is null)
            return Task.FromResult(Result.Fail($"Invalid {typeof(T).Name} payload"));
        return handler.HandleAsync(parsed, cancellationToken);
    }
}
