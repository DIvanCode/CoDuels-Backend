using FluentResults;

namespace Duely.Application.UseCases.Features.Outbox.Relay;

public interface IOutboxDispatcher
{
    Task<Result> DispatchAsync(Domain.Models.Outbox message, CancellationToken cancellationToken);
}
