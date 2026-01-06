using Duely.Application.Services.Outbox.Payloads;
using Duely.Domain.Models;
using FluentResults;

namespace Duely.Application.Services.Outbox.Relay;

public interface IOutboxHandler<TPayload> where TPayload : IOutboxPayload
{
    OutboxType Type { get; }
    Task<Result> HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
