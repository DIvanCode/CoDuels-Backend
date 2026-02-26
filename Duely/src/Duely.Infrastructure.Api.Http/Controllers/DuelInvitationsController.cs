using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Infrastructure.Api.Http.Requests.DuelInvitations;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/invitations")]
[Authorize]
public sealed class DuelInvitationsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DuelInvitationDto>>> GetIncomingAsync(CancellationToken cancellationToken)
    {
        var query = new GetIncomingDuelInvitationsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost]
    public async Task<ActionResult> CreateAsync(
        [FromBody] DuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDuelInvitationCommand
        {
            UserId = userContext.UserId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("cancel")]
    public async Task<ActionResult> CancelAsync(
        [FromBody] DuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelDuelInvitationCommand
        {
            UserId = userContext.UserId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("accept")]
    public async Task<ActionResult> AcceptAsync(
        [FromBody] DuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AcceptDuelInvitationCommand
        {
            UserId = userContext.UserId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("deny")]
    public async Task<ActionResult> DenyAsync(
        [FromBody] DuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DenyDuelInvitationCommand
        {
            UserId = userContext.UserId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("group")]
    public async Task<ActionResult> CreateGroupAsync(
        [FromBody] GroupDuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateGroupDuelInvitationCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId,
            User1Id = request.User1Id,
            User2Id = request.User2Id,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("group/accept")]
    public async Task<ActionResult> AcceptGroupAsync(
        [FromBody] GroupDuelAcceptRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AcceptGroupDuelInvitationCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId,
            OpponentNickname = request.OpponentNickname,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("group/cancel")]
    public async Task<ActionResult> CancelGroupAsync(
        [FromBody] GroupDuelInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelGroupDuelInvitationCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId,
            User1Id = request.User1Id,
            User2Id = request.User2Id,
            ConfigurationId = request.ConfigurationId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
