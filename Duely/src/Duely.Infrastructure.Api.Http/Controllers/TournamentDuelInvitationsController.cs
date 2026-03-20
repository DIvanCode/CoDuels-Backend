using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/tournament/invitations")]
[Authorize]
public sealed class TournamentDuelInvitationsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TournamentDuelInvitationDto>>> GetIncomingAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetIncomingTournamentDuelInvitationsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }
}
