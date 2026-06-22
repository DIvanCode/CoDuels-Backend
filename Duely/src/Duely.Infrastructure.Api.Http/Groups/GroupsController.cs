// using Duely.Application.UseCases.Dto.Groups;
// using Duely.Application.UseCases.Features.Groups;
// using Duely.Infrastructure.Api.Http.Groups.Requests;
// using MediatR;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
//
// namespace Duely.Infrastructure.Api.Http.Groups;
//
// [ApiController]
// [Route("groups")]
// [Authorize]
// internal sealed class GroupsController(IMediator mediator, IUserContext userContext) : ControllerBase
// {
//     [HttpPost]
//     public async Task<ActionResult<GroupShortDto>> CreateAsync(
//         [FromBody] CreateGroupRequest request,
//         CancellationToken cancellationToken)
//     {
//         var command = new CreateGroupCommand
//         {
//             UserId = userContext.UserId,
//             Name = request.Name
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpGet("{id:guid}")]
//     public async Task<ActionResult<GroupDto>> GetAsync(
//         [FromRoute] Guid id,
//         CancellationToken cancellationToken)
//     {
//         var query = new GetGroupQuery
//         {
//             UserId = userContext.UserId,
//             GroupId = id
//         };
//
//         var result = await mediator.Send(query, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpGet]
//     public async Task<ActionResult<List<GroupShortDto>>> GetListAsync(CancellationToken cancellationToken)
//     {
//         var query = new GetUserGroupsQuery
//         {
//             UserId = userContext.UserId
//         };
//
//         var result = await mediator.Send(query, cancellationToken);
//         return this.HandleResult(result);
//     }
//     
//     [HttpGet("pending")]
//     public async Task<ActionResult<List<GroupShortDto>>> GetPendingAsync(CancellationToken cancellationToken)
//     {
//         var query = new GetPendingGroupsQuery
//         {
//             UserId = userContext.UserId
//         };
//         
//         var result = await mediator.Send(query, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpPut("{id:guid}")]
//     public async Task<ActionResult<GroupShortDto>> UpdateAsync(
//         [FromRoute] Guid id,
//         [FromBody] UpdateGroupRequest request,
//         CancellationToken cancellationToken)
//     {
//         var command = new UpdateGroupCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = id,
//             Name = request.Name
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
// }
