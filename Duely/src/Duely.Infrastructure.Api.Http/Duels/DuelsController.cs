using Duely.Application.Handlers.Duels.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Duels;

[ApiController]
[Route("duels")]
[Authorize]
public sealed class DuelsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPut("{id:int}/ready")]
    public async Task<ActionResult> ReadyAsync(int id, CancellationToken cancellationToken)
    {
        var command = new SetReadyToDuelCommand
        {
            UserId = userContext.UserId,
            DuelId = id
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}