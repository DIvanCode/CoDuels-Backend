using System.Net;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.Gateway.Analyzer;
using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using Duely.Infrastructure.Gateway.Exesh;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests.Gateways;

public sealed class InternalAuthTests
{
    [Theory]
    [InlineData("INTERNAL_AUTH_KEY_LOCAL_VALUE")]
    [InlineData("overridden-test-key")]
    public async Task AllGatewayRequestsSendConfiguredKey(string key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InternalAuthKey"] = key,
            ["Taski:BaseUrl"] = "http://taski/",
            ["Exesh:BaseUrl"] = "http://exesh/",
            ["Analyzer:BaseUrl"] = "http://analyzer/"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupTasksGateway(configuration);
        services.SetupExeshGateway(configuration);
        services.SetupAnalyzerGateway(configuration);
        var handler = new RecordingHandler();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();
        var taski = provider.GetRequiredService<ITaskiClient>();
        var exesh = provider.GetRequiredService<IExeshClient>();
        var analyzer = provider.GetRequiredService<IAnalyzerClient>();

        Assert.True((await taski.TestSolutionAsync("task", "solution", "print(1)", Language.Python, default)).IsSuccess);
        Assert.True((await taski.GetTasksListAsync(default)).IsSuccess);
        Assert.True((await taski.GetSolutionEventsAsync("solution", 1, 10, default)).IsSuccess);
        Assert.True((await exesh.ExecuteAsync(new ExecuteCodeRequest("print(1)", "Python", "", 1, 2, "task", 1000, 256), default)).IsSuccess);
        Assert.True((await exesh.GetExecutionEventsAsync("execution", 1, 10, default)).IsSuccess);
        Assert.True((await analyzer.PredictAsync(new PredictRequest { Actions = [], UserRating = 1000 }, default)).IsSuccess);

        Assert.Equal(new[] { "/test", "/task/list", "/solutions/solution/messages", "/execute", "/executions/execution/messages", "/predict" }, handler.Requests.Select(r => r.Path));
        Assert.All(handler.Requests, request => Assert.Equal(new[] { key }, request.Keys));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Path, string[] Keys)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((path, request.Headers.TryGetValues("internal-auth", out var keys) ? keys.ToArray() : []));
            var body = path switch
            {
                "/execute" => "{\"status\":\"OK\",\"execution_id\":\"execution\"}",
                "/predict" => "{\"score\":0.1}",
                "/task/list" => "{\"tasks\":[]}",
                _ => "{\"status\":\"OK\",\"messages\":[]}"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
