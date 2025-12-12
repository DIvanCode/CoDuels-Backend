using Duely.Application.UseCases;
using Duely.Application.BackgroundJobs;
using Duely.Domain.Services;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Exesh;
using Duely.Infrastructure.MessageBus.Kafka;
using Hellang.Middleware.ProblemDetails;
using Duely.Application.UseCases.Features.Outbox.Relay;
using Duely.Application.UseCases.Features.Outbox.Handlers;
using Duely.Application.UseCases.Payloads;
using Duely.Application.UseCases.Features.RateLimiting;


AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Application
builder.Services.SetupUseCases(builder.Configuration);
builder.Services.AddScoped<IOutboxHandler<TestSolutionPayload>, TestSolutionHandler>();
builder.Services.AddScoped<IOutboxHandler<RunUserCodePayload>, RunUserCodeOutboxHandler>();
builder.Services.AddScoped<IOutboxHandler<SendMessagePayload>, SendMessageOutboxHandler>();
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
builder.Services.SetupBackgroundJobs(builder.Configuration);
builder.Services.AddScoped<ISubmissionRateLimiter, SubmissionRateLimiter>();
builder.Services.AddScoped<IRunUserCodeLimiter, RunUserCodeLimiter>();

// Domain
builder.Services.SetupDomainServices(builder.Configuration);

// Infrastructure
builder.Services.SetupApiHttp(builder.Configuration, builder.Environment);
builder.Services.SetupDataAccessEntityFramework(builder.Configuration);
builder.Services.SetupTasksGateway(builder.Configuration);
builder.Services.SetupExeshGateway(builder.Configuration);
builder.Services.SetupMessageBusKafka(builder.Configuration);

var app = builder.Build();

app.UseApiHttp();
app.UseProblemDetails();
app.Run();
