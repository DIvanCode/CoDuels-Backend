using Duely.Domain.Models.Outbox.Payloads;
using FluentResults;

namespace Duely.Application.Services.Outbox.Relay;

public interface IOutboxHandler<in TPayload> where TPayload : OutboxPayload
{
    Task<Result> HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
