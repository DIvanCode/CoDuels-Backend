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
    [HttpGet("~/duels/admin/submissions/testing")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<AdminSubmissionListItemDto>>> GetTestingForAdminAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetAllSubmissionsQuery { OnlyTesting = true };
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("~/duels/admin/submissions/all")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<AdminSubmissionListItemDto>>> GetAllForAdminAsync(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllSubmissionsQuery(), cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<SubmissionDto>> SendSubmissionAsync(
        [FromRoute] int duelId,
        [FromBody] SendSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendSubmissionCommand
        {
            DuelId = duelId,
            UserId = userContext.UserId,
            TaskKey = request.TaskKey,
            Solution = request.Solution,
            Language = request.Language
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<List<SubmissionListItemDto>>> GetUserSubmissionsAsync(
        [FromRoute] int duelId,
        [FromQuery] char taskKey,
        CancellationToken cancellationToken)
    {
        var query = new GetUserSubmissionsQuery
        {
            DuelId = duelId,
            UserId = userContext.UserId,
            TaskKey = taskKey,
            IsAdmin = userContext.IsAdmin()
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
            SubmissionId = submissionId,
            IsAdmin = userContext.IsAdmin()
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
