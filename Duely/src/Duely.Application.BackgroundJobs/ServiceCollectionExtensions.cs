using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Duely.Application.BackgroundJobs;

public static class ServiceCollectionExtensions
{
    public static void SetupBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DuelMakingJobOptions>(configuration.GetSection(DuelMakingJobOptions.SectionName));
        services.AddHostedService<DuelMakingJob>();

        services.Configure<DuelEndWatcherJobOptions>(configuration.GetSection(DuelEndWatcherJobOptions.SectionName));
        services.AddHostedService<DuelEndWatcherJob>();

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxJob>();
    }
}
