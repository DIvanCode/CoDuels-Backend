using Duely.Application.UseCases.Submissions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("api/duels/{duelId:int}")]
public class DuelController : ControllerBase
{
    private readonly IMediator _mediator;

    public DuelController(IMediator mediator) => _mediator = mediator;

    [HttpPost("submit")]
    public async Task<IActionResult> SendSubmissionAsync(
        [FromRoute] int duelId,
        [FromBody] SendSubmissionRequest request,
        CancellationToken cancellationToken
    )
    {

        if (!Request.Cookies.TryGetValue("UserId", out var userIdStr) ||
                !int.TryParse(userIdStr, out var userId))
        {
            return BadRequest(new { error = "Missing or invalid UserId cookie" });
        }

        var command = new SendSubmissionCommand
        {
            DuelId = duelId,
            UserId = userId,
            Code = request.Submission,
            Language = request.Language
        };

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailed)
        {
            return BadRequest(new {error = result.Errors.First().Message});
        }

        return Ok(new { submission_id = result.Value });

    }

    [HttpGet("submissions/{submissionId:int}")] 
    public async Task<ActionResult<SubmissionDto>> GetSubmissionAsync(
        [FromRoute] int duelId,
        [FromRoute] int submissionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubmissionQuery {
            SubmissionId = submissionId,
            DuelId = duelId
        }, cancellationToken);

        if (result.IsFailed)
        {
            return BadRequest(new {error = result.Errors.First().Message});
        }

        return result.Value;
    }
}
