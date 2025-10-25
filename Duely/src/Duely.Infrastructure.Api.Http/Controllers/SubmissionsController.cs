using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Infrastructure.Api.Http.Requests.Submissions;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/{duelId:int}/submissions")]
[Authorize]
public sealed class SubmissionsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubmissionDto>> SendSubmissionAsync(
        [FromRoute] int duelId,
        [FromBody] SendSubmissionRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new SendSubmissionCommand
        {
            DuelId = duelId,
            UserId = userContext.UserId,
            Code = request.Submission,
            Language = request.Language
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
    [HttpGet]
    public async Task<ActionResult<List<SubmissionListItemDto>>> GetUserSubmissionsAsync(
        [FromRoute] int duelId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserSubmissionsQuery
        {
            DuelId = duelId,
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
    [HttpGet("{submissionId:int}")] 
    public async Task<ActionResult<SubmissionDto>> GetSubmissionAsync(
        [FromRoute] int duelId,
        [FromRoute] int submissionId,
        CancellationToken cancellationToken)
    {
        var query = new GetSubmissionQuery
        {
            UserId = userContext.UserId,
            DuelId = duelId,
            SubmissionId = submissionId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
