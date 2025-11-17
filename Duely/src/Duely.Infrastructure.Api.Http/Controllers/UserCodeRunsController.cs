using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.UserCodeRuns;
using Duely.Infrastructure.Api.Http.Requests.UserCodeRuns;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/{duelId:int}/runs")]
[Authorize]
public sealed class UserCodeRunsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserCodeRunDto>> RunUserCodeAsync(
        [FromRoute] int duelId,
        [FromBody] RunUserCodeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RunUserCodeCommand
        {
            DuelId = duelId,
            UserId = userContext.UserId,
            Code = request.Solution,
            Language = request.Language,
            Input = request.Input
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<UserCodeRunListItemDto>>> GetUserRunsAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserCodeRunsQuery
        {
            DuelId = duelId,
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{runId:int}")]
    public async Task<ActionResult<UserCodeRunDto>> GetRunAsync(
        [FromRoute] int duelId,
        [FromRoute] int runId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserCodeRunQuery
        {
            DuelId = duelId,
            UserId = userContext.UserId,
            RunId = runId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
