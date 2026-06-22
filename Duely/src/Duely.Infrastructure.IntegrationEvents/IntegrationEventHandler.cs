using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;

namespace Duely.Infrastructure.IntegrationEvents;

internal interface IIntegrationEventHandler
{
    IntegrationEventType SupportedType { get; }
    
    Task<Result> Handle(IntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

internal interface IIntegrationEventHandler<in TIntegrationEvent> where TIntegrationEvent : IntegrationEvent
{
    Task<Result> Handle(TIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

internal abstract class IntegrationEventHandler<TIntegrationEvent>
    : IIntegrationEventHandler, IIntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{
    public abstract IntegrationEventType SupportedType { get; }
    
    public Task<Result> Handle(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent is TIntegrationEvent tIntegrationEvent)
        {
            return Handle(tIntegrationEvent, cancellationToken);
        }
        
        throw new InvalidOperationException("Некорректный обработчик интеграционного события.");
    }

    public abstract Task<Result> Handle(TIntegrationEvent configuration, CancellationToken cancellationToken);
}
