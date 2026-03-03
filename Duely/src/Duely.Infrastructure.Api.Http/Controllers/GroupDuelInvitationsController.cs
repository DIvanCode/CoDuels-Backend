using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Infrastructure.Api.Http.Requests.DuelInvitations;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duels/group/invitations")]
[Authorize]
public sealed class GroupDuelInvitationsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GroupDuelInvitationDto>>> GetIncomingAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetIncomingGroupDuelInvitationsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost]
    public async Task<ActionResult> CreateAsync(
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

    [HttpPost("accept")]
    public async Task<ActionResult> AcceptAsync(
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

    [HttpPost("cancel")]
    public async Task<ActionResult> CancelAsync(
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
