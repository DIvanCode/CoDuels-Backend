using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.Api.Http;

public static class ServiceCollectionExtensions
{
    public static void SetupApiHttp(this IServiceCollection services)
    {
        services.AddMvc();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }
}