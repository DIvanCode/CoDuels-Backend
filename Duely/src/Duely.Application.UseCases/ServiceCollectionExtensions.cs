using System.Reflection;
using Duely.Application.UseCases.Features.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.UseCases;

public static class ServiceCollectionExtensions
{
    public static void SetupUseCases(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
    }
}