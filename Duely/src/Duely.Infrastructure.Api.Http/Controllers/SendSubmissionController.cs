using Duely.Application.UseCases.Submissions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("api/duels/{duelId:int}/submit")]
public class SendSubmissionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SendSubmissionController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> SubmitAsync(
        [FromRoute] int duelId,
        [FromBody] SendSubmissionDto request,
        CancellationToken cancellationToken
    )
    {
        var userId = request.UserId; // взять из sse

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
            return BadRequest(result.Errors);
        }

        return Ok(new { submission_id = result.Value });

    }
}


public sealed class SendSubmissionDto
{
    public required string Submission { get; init; }
    public required string Language { get; init; }
    public int UserId { get; init; }= 0;
}