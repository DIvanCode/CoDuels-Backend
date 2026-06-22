using Duely.Infrastructure.IntegrationEvents.Models;

namespace Duely.Infrastructure.IntegrationEvents;

internal interface IIntegrationEventHandlerResolver
{
    IIntegrationEventHandler Resolve(IntegrationEventType type);
}

internal sealed class IntegrationEventHandlerResolver : IIntegrationEventHandlerResolver
{
    private readonly IReadOnlyDictionary<IntegrationEventType, IIntegrationEventHandler> _handlers;

    public IntegrationEventHandlerResolver(IEnumerable<IIntegrationEventHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
    }
    
    public IIntegrationEventHandler Resolve(IntegrationEventType type)
    {
        if (_handlers.TryGetValue(type, out var handler))
        {
            return handler;
        }
        
        throw new InvalidOperationException($"Обработчик интеграционного события {type} на зарегистрирован.");
    }
}
