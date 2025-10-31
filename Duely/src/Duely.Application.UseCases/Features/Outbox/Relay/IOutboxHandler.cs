using FluentResults;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Outbox.Relay;

public interface IOutboxHandler<TPayload>
{
    OutboxType Type { get; }
    Task<Result> HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
