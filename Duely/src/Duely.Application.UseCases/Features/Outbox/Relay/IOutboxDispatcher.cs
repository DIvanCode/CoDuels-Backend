using FluentResults;
using Duely.Domain.Models;
namespace Duely.Application.UseCases.Features.Outbox.Relay;

public interface IOutboxDispatcher
{
    Task<Result> DispatchAsync(OutboxMessage message, CancellationToken cancellationToken);
}
