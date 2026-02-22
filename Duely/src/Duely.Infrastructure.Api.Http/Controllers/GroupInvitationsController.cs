using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Groups;
using Duely.Infrastructure.Api.Http.Requests.Groups;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("groups/invitations")]
[Authorize]
public sealed class GroupInvitationsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> InviteUserAsync(
        [FromBody] InviteGroupUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new InviteUserCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId,
            InvitedUserId = request.UserId,
            Role = request.Role
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<List<GroupInvitationDto>>> GetIncomingAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetGroupInvitationsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("accept")]
    public async Task<ActionResult> AcceptAsync(
        [FromBody] GroupInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AcceptInviteCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("deny")]
    public async Task<ActionResult> DenyAsync(
        [FromBody] GroupInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DenyInviteCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("cancel")]
    public async Task<ActionResult> CancelAsync(
        [FromBody] CancelGroupInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelInviteCommand
        {
            UserId = userContext.UserId,
            GroupId = request.GroupId,
            InvitedUserId = request.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
