using Duely.Domain.Models;
using FluentResults;

namespace Duely.Application.Services.Outbox.Relay;

public interface IOutboxDispatcher
{
    Task<Result> DispatchAsync(OutboxMessage message, CancellationToken cancellationToken);
}
