using Duely.Infrastructure.Api.Http.Services.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http;

public static class WebApplicationExtensions
{
    public static void UseApiHttp(this WebApplication app)
    {
        var webSocketOptions = app.Services.GetRequiredService<IOptions<WebSocketConnectionOptions>>();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMilliseconds(webSocketOptions.Value.KeepAliveIntervalMs)
        });

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors("AllowAll");

        app.MapControllers();
    }
}
