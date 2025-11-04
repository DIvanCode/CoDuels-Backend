using MediatR;
using Microsoft.AspNetCore.Mvc;
using Duely.Infrastructure.Api.Http.Requests.Users;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Duely.Infrastructure.Api.Http.Services;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(IMediator mediator, IUserContext userContext) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand
        {
            Nickname = request.Nickname,
            Password = request.Password
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand
        {
            Nickname = request.Nickname,
            Password = request.Password
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("iam")]
    [Authorize]
    public async Task<ActionResult<UserDto>> IamAsync(CancellationToken cancellationToken)
    {
        var query = new IamQuery
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{userId:int}")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetAsync(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserQuery
        {
            UserId = userId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenDto>> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefreshCommand
        {
            RefreshToken = request.RefreshToken
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }
}
