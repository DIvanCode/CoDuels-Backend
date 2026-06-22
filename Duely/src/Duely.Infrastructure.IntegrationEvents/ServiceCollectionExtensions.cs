using Duely.Infrastructure.IntegrationEvents.Handlers;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.IntegrationEvents;

public static class ServiceCollectionExtensions
{
    public static void SetupIntegrationEvents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IntegrationEventsProcessorOptions>(
            configuration.GetSection(IntegrationEventsProcessorOptions.SectionName));
        
        services.AddHostedService<IntegrationEventsProcessor>();
        
        services.AddScoped<IIntegrationEventHandlerResolver, IntegrationEventHandlerResolver>();
        
        services.AddScoped<IIntegrationEventHandler, UserCreatedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<UserCreatedIntegrationEvent>, UserCreatedIntegrationEventHandler>();
    }
}
