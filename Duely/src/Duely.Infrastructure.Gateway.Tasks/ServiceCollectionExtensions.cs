using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Gateway.Tasks;

public static class ServiceCollectionExtensions
{
    public static void SetupTasksGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TaskiOptions>(configuration.GetSection(TaskiOptions.SectionName));

        services.AddHttpClient<ITaskiClient, TaskiClient>((sp, client) =>
        {  
            var options = sp.GetRequiredService<IOptions<TaskiOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
        });
    }
}
