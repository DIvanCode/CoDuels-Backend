using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.UseCases;

public static class ServiceCollectionExtensions
{
    public static void SetupUseCases(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    }
}