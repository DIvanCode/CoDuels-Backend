using Duely.Application.Services.Outbox.Handlers;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Services.Outbox.Relay;
using Duely.Application.Services.RateLimiting;
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
        services.AddScoped<IOutboxHandler<RunUserCodePayload>, RunUserCodeOutboxHandler>();
        services.AddScoped<IOutboxHandler<SendMessagePayload>, SendMessageOutboxHandler>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
    }
}