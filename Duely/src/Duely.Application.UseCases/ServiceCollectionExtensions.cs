using System.Reflection;
using Duely.Application.UseCases.Helpers;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.UseCases;

public static class ServiceCollectionExtensions
{
    public static void SetupUseCases(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        foreach (var mapperType in GetConcreteTypes<ITournamentDetailsMapper>(typeof(ServiceCollectionExtensions).Assembly))
        {
            services.AddScoped(typeof(ITournamentDetailsMapper), mapperType);
        }
        services.AddScoped<ITournamentDetailsMapperResolver, TournamentDetailsMapperResolver>();
    }

    private static IEnumerable<Type> GetConcreteTypes<TService>(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => typeof(TService).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false });
    }
}
