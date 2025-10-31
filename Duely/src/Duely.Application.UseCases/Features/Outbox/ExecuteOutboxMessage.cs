using FluentResults;
using MediatR;
using Duely.Application.UseCases.Features.Outbox.Relay;
using OutboxEntity = Duely.Domain.Models.Outbox;

namespace Duely.Application.UseCases.Features.Outbox;
public sealed record ExecuteOutboxMessageCommand(OutboxEntity Message) : IRequest<Result>;

public sealed class ExecuteOutboxMessageHandler(IOutboxDispatcher dispatcher): IRequestHandler<ExecuteOutboxMessageCommand, Result>
{
    public Task<Result> Handle(ExecuteOutboxMessageCommand request, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(request.Message, cancellationToken);
}
