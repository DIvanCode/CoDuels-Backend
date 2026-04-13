using Duely.Application.UseCases.Features.UserActions;
using Duely.Infrastructure.Api.Http.Requests.UserActions;
using Duely.Infrastructure.Api.Http.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("actions")]
[Authorize]
public sealed class UserActionsController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> SaveAsync(
        [FromBody] SaveUserActionsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveUserActionsCommand
        {
            UserId = userContext.UserId,
            Actions = request.Actions
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
