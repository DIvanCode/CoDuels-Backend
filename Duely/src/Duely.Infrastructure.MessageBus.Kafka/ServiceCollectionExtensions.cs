using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.MessageBus.Kafka;

public static class ServiceCollectionExtensions
{
    public static void SetupTaskiConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddHostedService<TaskiSubmissionStatusConsumer>();
    }

    public static void SetupExeshConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddHostedService<ExeshSubmissionStatusConsumer>();
    }
}
