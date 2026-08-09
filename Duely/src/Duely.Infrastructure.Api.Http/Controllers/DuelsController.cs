using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Duels;
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
    IUserContext userContext,
    IRatingManager ratingManager) : ControllerBase
{
    [HttpGet("admin/pending")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<PendingDuelDto>>> GetPendingForAdminAsync(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPendingDuelsQuery(), cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("admin/ranked-searchers")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<RankedDuelSearcherDto>>> GetRankedSearchersForAdminAsync(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRankedDuelSearchersQuery(), cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("admin/active")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<DuelDto>>> GetAllActiveForAdminAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetDuelsByStatusQuery { Status = DuelStatus.InProgress };
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("admin/finished")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<DuelDto>>> GetAllFinishedForAdminAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetDuelsByStatusQuery { Status = DuelStatus.Finished };
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("task-level-rating-ranges")]
    public ActionResult<IReadOnlyDictionary<int, string>> GetTaskLevelRatingRanges()
    {
        return Ok(ratingManager.GetTaskLevelRatingRanges());
    }

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

    [HttpPost("cancel")]
    public async Task<ActionResult> CancelPendingAsync(CancellationToken cancellationToken)
    {
        var command = new CancelPendingDuelsCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
