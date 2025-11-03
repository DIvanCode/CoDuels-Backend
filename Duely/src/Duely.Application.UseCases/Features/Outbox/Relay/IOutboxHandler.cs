using FluentResults;
using Duely.Domain.Models;
using Duely.Application.UseCases.Payloads;
namespace Duely.Application.UseCases.Features.Outbox.Relay;

public interface IOutboxHandler<TPayload> where TPayload : IOutboxPayload
{
    OutboxType Type { get; }
    Task<Result> HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
