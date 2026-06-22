// using Duely.Application.UseCases.Features.Duels.RankedSearches;
// using MediatR;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
//
// namespace Duely.Infrastructure.Api.Http.Duels;
//
// [ApiController]
// [Route("duels/ranked-searches")]
// [Authorize]
// internal sealed class DuelsController(IMediator mediator, IUserContext userContext) : ControllerBase
// {
//     [HttpPost]
//     public async Task<ActionResult> StartAsync(CancellationToken cancellationToken)
//     {
//         var command = new StartRankedSearchCommand
//         {
//             UserId = userContext.UserId
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
//     
//     [HttpDelete]
//     public async Task<ActionResult> CancelAsync(CancellationToken cancellationToken)
//     {
//         var command = new CancelRankedSearchCommand
//         {
//             UserId = userContext.UserId
//         };
//
//         var result = await mediator.Send(command, cancellationToken);
//         return this.HandleResult(result);
//     }
// }
