using Duely.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Duely.Infrastructure.Api.Sse;

[ApiController]
[Route("api/duels/events")]
public class DuelsEventsController : ControllerBase
{
    private readonly SseConnectionManager _connections;
    private readonly DuelSettings _settings;
    
    public DuelsEventsController(SseConnectionManager connections, IOptions<DuelSettings> options)
    {
        _connections = connections;
        _settings = options.Value;
    }

    [HttpGet]
    public async Task Connect(
        [FromQuery(Name = "user_id")] int userId, 
        CancellationToken cancellationToken)
    {

        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "keep-alive");

        _connections.AddConnection(userId, HttpContext.Response);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(_settings.UserIdCookieDays)
        };
        Response.Cookies.Append("UserId", userId.ToString(), cookieOptions);

        try {
            while (!cancellationToken.IsCancellationRequested) {
                await Response.WriteAsync(":\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(_settings.SsePingIntervalMs), cancellationToken);
            }
        }
        catch (TaskCanceledException) { }
        finally {
            _connections.RemoveConnection(userId);
        }
    }
}