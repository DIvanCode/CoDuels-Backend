using Microsoft.AspNetCore.Mvc;
using MediatR;
using Duely.Application.UseCases.Submissions;
using FluentResults;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/{duelId:int}/submissions/{submissionId:int}")]
public class GetSubmissionController : ControllerBase
{
    private readonly IMediator _mediator;

    public GetSubmissionController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetSubmissionAsync(
        [FromRoute] int duelId,
        [FromRoute] int submissionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubmissionQuery {
            SubmissionId = submissionId,
            DuelId = duelId
        });

        if (result.IsFailed)
        {
            return BadRequest(new {error = result.Errors.First().Message});
        }

        return Ok(result.Value);
    }
}
