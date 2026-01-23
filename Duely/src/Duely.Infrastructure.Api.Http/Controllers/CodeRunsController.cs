using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.CodeRuns;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("code-runs")]
[Authorize]
public sealed class RunsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CodeRunDto>> RunUserCodeAsync(
        [FromBody] CreateCodeRunCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCodeRunCommand
        {
            UserId = userContext.UserId,
            Code = request.Code,
            Language = request.Language,
            Input = request.Input
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CodeRunDto>> GetRunAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetCodeRunQuery
        {
            Id = id,
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
