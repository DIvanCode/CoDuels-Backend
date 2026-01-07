using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Infrastructure.Api.Http.Services;
using Duely.Infrastructure.Api.Http.Services.Sse;
using Duely.Infrastructure.Api.Http.Requests.Duels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels")]
[Authorize]
public sealed class DuelsController(
    IMediator mediator,
    IUserContext userContext,
    ISseConnectionManager connections,
    IOptions<SseConnectionOptions> options,
    ILogger<DuelsController> logger) : ControllerBase
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
    
    [HttpGet("current")]
    public async Task<ActionResult<DuelDto>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var query = new GetCurrentDuelQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<DuelDto>>> GetHistoryAsync(
        [FromQuery] int userId,
        CancellationToken cancellationToken)
    {
        var query = new GetDuelsHistoryQuery
        {
            UserId = userId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<PendingDuelRequestsDto>> GetPendingRequestsAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetPendingDuelRequestsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<DuelRequestDto>> CreateRequestAsync(
        [FromBody] CreateDuelRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDuelRequestCommand
        {
            UserId = userContext.UserId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("requests/{duelId:int}/accept")]
    public async Task<ActionResult> AcceptRequestAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var command = new AcceptDuelRequestCommand
        {
            UserId = userContext.UserId,
            DuelId = duelId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("requests/{duelId:int}/deny")]
    public async Task<ActionResult> DenyRequestAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var command = new DenyDuelRequestCommand
        {
            UserId = userContext.UserId,
            DuelId = duelId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("requests/{duelId:int}/cancel")]
    public async Task<ActionResult> CancelRequestAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var command = new CancelDuelRequestCommand
        {
            UserId = userContext.UserId,
            DuelId = duelId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("connect")]
    public async Task<IActionResult> ConnectAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Connected user {UserId}", userContext.UserId);
        
        var query = new GetCurrentDuelQuery
        {
            UserId = userContext.UserId
        };
        
        var currentDuel = await mediator.Send(query, cancellationToken);
        if (currentDuel.IsFailed)
        {
            var command = new AddUserCommand
            {
                UserId = userContext.UserId
            };
            var addUserResult = await mediator.Send(command, cancellationToken);
            if (addUserResult.IsFailed)
            {
                foreach (var error in addUserResult.Errors)
                {
                    logger.LogWarning("SSE connect failed: {Reason}", error.Message);
                }

                return this.HandleResult(addUserResult);
            }
        }
        
        connections.AddConnection(userContext.UserId, HttpContext.Response);
        
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Connection", "keep-alive");
        
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
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cancellationToken = cancellationTokenSource.Token;
           
            currentDuel = await mediator.Send(query, cancellationToken);
            if (currentDuel.IsFailed)
            {
                var command = new RemoveUserCommand
                {
                    UserId = userContext.UserId
                };
                await mediator.Send(command, cancellationToken);    
            }

            logger.LogInformation("Disconnected user {UserId}", userContext.UserId);

            connections.RemoveConnection(userContext.UserId);
        }
        
        return Ok();
    }
}
