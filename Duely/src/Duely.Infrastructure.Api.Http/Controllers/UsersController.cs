using MediatR;
using Microsoft.AspNetCore.Mvc;
using Duely.Infrastructure.Api.Http.Requests.Users;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Users;
using Duely.Application.UseCases.Features.Duels;
using Microsoft.AspNetCore.Authorization;
using Duely.Infrastructure.Api.Http.Services;
using Duely.Infrastructure.Api.Http.Services.WebSockets;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(
    IMediator mediator,
    IUserContext userContext,
    IUserWebSocketHandler webSocketHandler) : ControllerBase
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
        var query = new GetUserQuery
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
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand
        {
            RefreshToken = request.RefreshToken
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("ticket")]
    [Authorize]
    public async Task<ActionResult<TicketDto>> CreateTicketAsync(CancellationToken cancellationToken)
    {
        var command = new CreateTicketCommand
        {
            UserId = userContext.UserId
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("connect")]
    [AllowAnonymous]
    public async Task<IActionResult> ConnectAsync(
        [FromQuery] string ticket,
        CancellationToken cancellationToken)
    {
        var userResult = await mediator.Send(
            new GetUserByTicketCommand { Ticket = ticket },
            cancellationToken);
        if (userResult.IsFailed)
        {
            return this.HandleResult(userResult.ToResult());
        }

        return await webSocketHandler.HandleConnectionAsync(
            HttpContext,
            userResult.Value,
            cancellationToken);
    }
}
