using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels")]
[Authorize]
public sealed class DuelsController(
    IMediator mediator,
    IUserContext userContext) : ControllerBase
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
    
    [HttpGet("active")]
    public async Task<ActionResult<DuelDto>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var query = new GetActiveDuelQuery
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

    [HttpPost("search")]
    public async Task<ActionResult> SearchAsync(CancellationToken cancellationToken)
    {
        var command = new StartDuelSearchCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("search/cancel")]
    public async Task<ActionResult> CancelSearchAsync(CancellationToken cancellationToken)
    {
        var command = new CancelDuelSearchCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
