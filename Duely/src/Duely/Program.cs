using Duely.Application.UseCases;
using Duely.Application.Configuration;
using Duely.Application.BackgroundJobs;
using Duely.Domain.Services;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Microsoft.Extensions.Options;

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

builder.Services.Configure<DuelSettings>(builder.Configuration.GetSection(DuelSettings.SectionName));

var app = builder.Build();

app.UseApiHttp();
app.Run();