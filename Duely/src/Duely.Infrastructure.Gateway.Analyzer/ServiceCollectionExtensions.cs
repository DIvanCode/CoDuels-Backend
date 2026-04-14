using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Gateway.Analyzer;

public static class ServiceCollectionExtensions
{
    public static void SetupAnalyzerGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnalyzerOptions>(configuration.GetSection(AnalyzerOptions.SectionName));

        services.AddHttpClient<IAnalyzerClient, AnalyzerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AnalyzerOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
        });
    }
}
