using Duely.Application.Handlers.Duels.Models;
using Duely.Application.Handlers.Duels.UseCases.Submissions;
using Duely.Infrastructure.Api.Http.Duels.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Duels;

[ApiController]
[Route("duels/{duelId:int}/submissions")]
[Authorize]
public sealed class SubmissionsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubmissionDto>> CreateAsync(
        int duelId,
        [FromBody] CreateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSubmissionCommand
        {
            UserId = userContext.UserId,
            DuelId = duelId,
            ProblemPosition = request.ProblemPosition,
            Source = request.Source,
            Language = request.Language
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
