using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Groups;
using Duely.Infrastructure.Api.Http.Requests.Groups;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("groups")]
[Authorize]
public sealed class GroupsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateAsync(
        [FromBody] CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateGroupCommand
        {
            UserId = userContext.UserId,
            Name = request.Name
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GroupDto>> GetAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetGroupQuery
        {
            UserId = userContext.UserId,
            GroupId = id
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<GroupDto>>> GetListAsync(CancellationToken cancellationToken)
    {
        var query = new GetUserGroupsQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GroupDto>> UpdateAsync(
        [FromRoute] int id,
        [FromBody] UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateGroupCommand
        {
            UserId = userContext.UserId,
            GroupId = id,
            Name = request.Name
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
