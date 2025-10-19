using Duely.Application.UseCases;
using Duely.Application.Configuration;
using Duely.Application.BackgroundJobs;
using Duely.Domain.Services;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Api.Sse;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.MessageBus.Kafka;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.SetupApiHttp();
builder.Services.SetupUseCases();
builder.Services.SetupDataAccessEntityFramework(builder.Configuration);
builder.Services.Configure<TaskiOptions>(builder.Configuration.GetSection("Taski"));
builder.Services.AddHttpClient<ITaskiClient, TaskiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<TaskiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddSingleton<IDuelManager, DuelManager>();
builder.Services.AddHostedService<DuelMakingJob>();
builder.Services.AddHostedService<DuelEndWatcherJob>();

builder.Services.Configure<DuelSettings>(builder.Configuration.GetSection(DuelSettings.SectionName));

builder.Services.AddSingleton<SseConnectionManager>();
builder.Services.AddSingleton<IMessageSender, SseMessageSender>();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka:OrderCreated"));
builder.Services.AddHostedService<TaskiSubmissionStatusConsumer>();

var app = builder.Build();

app.UseApiHttp();
app.Run();