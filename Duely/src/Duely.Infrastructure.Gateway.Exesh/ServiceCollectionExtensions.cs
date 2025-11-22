using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Gateway.Exesh;

public static class ServiceCollectionExtensions
{
    public static void SetupExeshGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExeshOptions>(configuration.GetSection(ExeshOptions.SectionName));

        services.AddHttpClient<IExeshClient, ExeshClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ExeshOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
        });
    }
}
