using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Infrastructure.Api.Http.Requests.Duels;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("requests")]
[Authorize]
public sealed class DuelRequestsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpGet]
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

    [HttpPost]
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

    [HttpPost("{duelId:int}/accept")]
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

    [HttpPost("{duelId:int}/deny")]
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

    [HttpPost("{duelId:int}/cancel")]
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
}
