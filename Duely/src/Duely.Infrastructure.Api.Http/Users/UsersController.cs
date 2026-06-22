using Duely.Application.Handlers.Users.Models;
using Duely.Application.Handlers.Users.UseCases;
using Duely.Domain.Kernel.Errors;
using Duely.Infrastructure.Api.Http.Users.Requests;
using Duely.Infrastructure.Api.Http.Users.Services.AuthToken;
using Duely.Infrastructure.Api.Http.Users.Services.IdentityTicket;
using Duely.Infrastructure.Api.Http.Users.Services.RefreshToken;
using Duely.Infrastructure.Api.Http.Users.Services.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Users;

[ApiController]
[Route("users")]
public sealed class UsersController(
    IMediator mediator,
    IAuthTokenService authTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IIdentityTicketService identityTicketService,
    IUserContext userContext,
    IUserWebSocketHandler webSocketHandler)
    : ControllerBase
{
    private const string RefreshTokenCookieKey = "refresh_token";
    
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
    public async Task<ActionResult<string>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var refreshToken = refreshTokenService.GenerateRefreshToken();
        var command = new LoginCommand
        {
            Nickname = request.Nickname,
            Password = request.Password,
            RefreshToken = refreshToken
        };

        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailed)
        {
            return this.HandleErrorResult(result.ToResult());
        }
        
        AppendRefreshTokenCookie(refreshToken);
        
        var authToken = authTokenService.GenerateAuthToken(result.Value);
        return authToken;
    }

    [HttpGet("iam")]
    [Authorize]
    public async Task<ActionResult<UserDto>> IamAsync(CancellationToken cancellationToken)
    {
        var query = new GetUserQuery
        {
            Id = userContext.UserId,
            Nickname = null
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetAsync(
        [FromQuery] int? id,
        [FromQuery] string? nickname,
        CancellationToken cancellationToken)
    {
        var query = new GetUserQuery
        {
            Id = id,
            Nickname = nickname
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<string>> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieKey, out var refreshToken))
        {
            return this.HandleResult(new ForbiddenError());
        }
        
        var newRefreshToken = refreshTokenService.GenerateRefreshToken();
        var command = new ExchangeRefreshTokenCommand
        {
            RefreshToken = refreshToken,
            NewRefreshToken = newRefreshToken
        };

        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailed)
        {
            return this.HandleErrorResult(result.ToResult());
        }
        
        
        AppendRefreshTokenCookie(newRefreshToken);
        
        var authToken = authTokenService.GenerateAuthToken(result.Value);
        return authToken;
    }

    [HttpPost("identity-ticket")]
    [Authorize]
    public async Task<ActionResult<string>> CreateIdentityTicketAsync(CancellationToken cancellationToken)
    {
        var identityTicket = identityTicketService.GenerateIdentityTicket();
        var command = new SetIdentityTicketCommand
        {
            UserId = userContext.UserId,
            IdentityTicket = identityTicket
        };

        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailed)
        {
            return this.HandleErrorResult(result);
        }

        return identityTicket;
    }

    [HttpGet("connect")]
    public async Task<IActionResult> ConnectAsync(
        [FromQuery] string ticket,
        CancellationToken cancellationToken)
    {
        var command = new UseIdentityTicketCommand
        {
            IdentityTicket = ticket
        };
        
        var userResult = await mediator.Send(command, cancellationToken);
        if (userResult.IsFailed)
        {
            return this.HandleResult(userResult.ToResult());
        }

        return await webSocketHandler.HandleConnectionAsync(
            HttpContext,
            userResult.Value,
            cancellationToken);
    }

    private void AppendRefreshTokenCookie(string value)
    {
        var expirationDate = DateTimeOffset.UtcNow.AddDays(refreshTokenOptions.Value.ExpiresDays);
        Response.Cookies.Append(RefreshTokenCookieKey, value, new CookieOptions
        {
            Expires = expirationDate,
            HttpOnly = true, // Prevents JavaScript access (XSS protection)
            Secure = true, // Forces HTTPS transmission
            SameSite = SameSiteMode.Strict // Prevents CSRF attacks
        });
    }
}
