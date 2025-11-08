using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Infrastructure.Api.Http.Services;
using Duely.Infrastructure.Api.Http.Services.Sse;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels")]
[Authorize]
public sealed class DuelsController(
    IMediator mediator,
    IUserContext userContext,
    ISseConnectionManager connections,
    IOptions<SseConnectionOptions> options) : ControllerBase
{
    [HttpGet("{duelId:int}")]
    public async Task<ActionResult<DuelDto>> GetAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var query = new GetDuelQuery
        {
            UserId = userContext.UserId,
            DuelId = duelId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<DuelHistoryItemDto>>> GetUserDuelsHistoryAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetUserDuelsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("connect")]
    public async Task Connect(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Connection", "keep-alive");

        connections.AddConnection(userContext.UserId, HttpContext.Response);

        await mediator.Send(new AddUserCommand { UserId = userContext.UserId }, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Response.WriteAsync(":\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(options.Value.SsePingIntervalMs), cancellationToken);
            }
        }
        finally
        {
            Console.WriteLine($"==== Disconnect user {userContext.UserId} ====");
            connections.RemoveConnection(userContext.UserId);
        }
    }
}
