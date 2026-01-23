using Duely.Application.Services.Outbox.Handlers;
using Duely.Application.Services.Outbox.Relay;
using Duely.Application.Services.RateLimiting;
using Duely.Domain.Models.Outbox.Payloads;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.Services;

public static class ServiceCollectionExtensions
{
    public static void SetupApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        
        services.AddScoped<ISubmissionRateLimiter, SubmissionRateLimiter>();
        services.AddScoped<IRunUserCodeLimiter, RunUserCodeLimiter>();
        
        services.AddScoped<IOutboxHandler<TestSolutionPayload>, TestSolutionHandler>();
        services.AddScoped<IOutboxHandler<RunCodePayload>, RunCodeOutboxHandler>();
        services.AddScoped<IOutboxHandler<SendMessagePayload>, SendMessageOutboxHandler>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
    }
}
