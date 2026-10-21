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
    [HttpGet("global")]
    public async Task<ActionResult<List<TournamentDto>>> GetGlobalAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGlobalTournamentsQuery(), cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("admin/active")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<TournamentDto>>> GetActiveForAdminAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetTournamentsByStateQuery { Finished = false };
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("admin/finished")]
    [Authorize(Policy = AuthorizationPolicies.OnlyAdmin)]
    public async Task<ActionResult<List<TournamentDto>>> GetFinishedForAdminAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetTournamentsByStateQuery { Finished = true };
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentDetailsDto>> GetAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetTournamentQuery
        {
            UserId = userContext.UserId,
            TournamentId = id,
            IsAdmin = userContext.IsAdmin()
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
            DuelConfigurationId = request.DuelConfigurationId,
            Description = request.Description,
            StartsAt = request.StartsAt,
            IsAdmin = userContext.IsAdmin()
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

    [HttpPost("{id:int}/register")]
    public async Task<ActionResult<TournamentDto>> RegisterAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ChangeGlobalTournamentRegistrationCommand
        {
            TournamentId = id,
            UserId = userContext.UserId,
            Join = true
        }, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("{id:int}/leave")]
    public async Task<ActionResult<TournamentDto>> LeaveAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ChangeGlobalTournamentRegistrationCommand
        {
            TournamentId = id,
            UserId = userContext.UserId,
            Join = false
        }, cancellationToken);
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
