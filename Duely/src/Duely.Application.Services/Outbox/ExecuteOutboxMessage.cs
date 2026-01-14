using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using FluentResults;
using MediatR;

namespace Duely.Application.Services.Outbox;

public sealed record ExecuteOutboxMessageCommand(OutboxMessage Message) : IRequest<Result>;

public sealed class ExecuteOutboxMessageHandler(IOutboxDispatcher dispatcher)
    : IRequestHandler<ExecuteOutboxMessageCommand, Result>
{
    public Task<Result> Handle(ExecuteOutboxMessageCommand request, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(request.Message, cancellationToken);
}
