using Duely.Application.Handlers.Duels.Models;
using Duely.Application.Handlers.Duels.UseCases.RankedDuels;
using Duely.Application.Handlers.Duels.UseCases.SearchRankedDuels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Duels;

[ApiController]
[Route("duels/ranked")]
[Authorize]
public sealed class RankedDuelsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult> StartSearchAsync(CancellationToken cancellationToken)
    {
        var command = new StartRankedDuelSearchCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
    
    [HttpDelete("search")]
    public async Task<ActionResult> CancelSearchAsync(CancellationToken cancellationToken)
    {
        var command = new CancelRankedDuelSearchCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
    
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DuelDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var query = new GetRankedDuelQuery
        {
            UserId = userContext.UserId,
            DuelId = id
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
