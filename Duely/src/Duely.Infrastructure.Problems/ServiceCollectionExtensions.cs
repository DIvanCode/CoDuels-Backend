using Duely.Infrastructure.Problems.Abstracts;
using Duely.Infrastructure.Problems.Taski;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Problems;

public static class ServiceCollectionExtensions
{
    public static void SetupProblems(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IProblemsGateway, ProblemsGateway>();
        
        services.Configure<ProblemsSynchronizerOptions>(configuration.GetSection(ProblemsSynchronizerOptions.SectionName));
        services.AddHostedService<ProblemsSynchronizer>();
        
        SetupTaskiGatewayAdapter(services, configuration);
    }

    private static void SetupTaskiGatewayAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        var optionsSection = configuration.GetSection(TaskiOptions.SectionName);
        var options = optionsSection.Get<TaskiOptions>();
        var isEnabled = options?.IsEnabled ?? false;
        if (!isEnabled)
        {
            return;
        }
        
        services.Configure<TaskiOptions>(optionsSection);
        
        services.AddHttpClient<IProblemsGatewayAdapter, TaskiProblemsGatewayAdapter>((sp, client) =>
        {  
            var taskiOptions = sp.GetRequiredService<IOptions<TaskiOptions>>();
            client.BaseAddress = new Uri(taskiOptions.Value.BaseUrl);
        });
    }
}
