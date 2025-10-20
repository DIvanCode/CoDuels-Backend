using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.Api.Http;

public static class ServiceCollectionExtensions
{
    public static void SetupApiHttp(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddMvc();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }
}