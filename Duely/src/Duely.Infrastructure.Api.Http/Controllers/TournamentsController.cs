using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Infrastructure.Api.Http.Requests.Tournaments;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("tournaments")]
[Authorize]
public sealed class TournamentsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentDetailsDto>> GetAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetTournamentQuery
        {
            UserId = userContext.UserId,
            TournamentId = id
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<TournamentDto>> CreateAsync(
        [FromBody] CreateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTournamentCommand
        {
            UserId = userContext.UserId,
            Name = request.Name,
            GroupId = request.GroupId,
            MatchmakingType = request.MatchmakingType,
            Participants = request.Participants,
            DuelConfigurationId = request.DuelConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<TournamentDto>> StartAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var command = new StartTournamentCommand
        {
            UserId = userContext.UserId,
            TournamentId = id
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("{id:int}/duels/accept")]
    public async Task<ActionResult> AcceptDuelAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var command = new AcceptTournamentDuelCommand
        {
            UserId = userContext.UserId,
            TournamentId = id
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
