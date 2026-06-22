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
// [Route("groups/{groupId:guid}/memberships")]
// [Authorize]
// internal sealed class GroupMembershipsController(IMediator mediator, IUserContext userContext) : ControllerBase
// {
//     [HttpPost]
//     public async Task<ActionResult<GroupMembershipShortDto>> CreateAsync(
//         [FromRoute] Guid groupId,
//         [FromBody] CreateGroupMembershipRequest request,
//         CancellationToken cancellationToken)
//     {
//         var command = new CreateGroupMembershipCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = groupId,
//             TargetUserId = request.UserId,
//             TargetUserRole = request.Role
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpPut]
//     public async Task<ActionResult<GroupMembershipShortDto>> UpdateAsync(
//         [FromRoute] Guid groupId,
//         [FromBody] UpdateGroupMembershipRequest request,
//         CancellationToken cancellationToken)
//     {
//         var command = new UpdateGroupMembershipCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = groupId,
//             TargetUserId = request.UserId,
//             TargetRole = request.Role
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpPost("confirm")]
//     public async Task<ActionResult<GroupMembershipShortDto>> ConfirmAsync(
//         [FromRoute] Guid groupId,
//         CancellationToken cancellationToken)
//     {
//         var command = new ConfirmGroupMembershipCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = groupId
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpPost("decline")]
//     public async Task<IActionResult> DeclineAsync(
//         [FromRoute] Guid groupId,
//         CancellationToken cancellationToken)
//     {
//         var command = new DeclineGroupMembershipCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = groupId
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//
//     [HttpDelete]
//     public async Task<IActionResult> DeleteAsync(
//         [FromRoute] Guid groupId,
//         [FromBody] DeleteGroupMembershipRequest request,
//         CancellationToken cancellationToken)
//     {
//         var command = new DeleteGroupMembershipCommand
//         {
//             UserId = userContext.UserId,
//             GroupId = groupId,
//             TargetUserId = request.UserId
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
// }
