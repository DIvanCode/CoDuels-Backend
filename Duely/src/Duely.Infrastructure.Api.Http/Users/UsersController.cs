using Duely.Application.UseCases.Dto.Users;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Common.Errors;
using Duely.Infrastructure.Api.Http.Users.Requests;
using Duely.Infrastructure.Api.Http.Users.Services.AuthToken;
using Duely.Infrastructure.Api.Http.Users.Services.IdentityTicket;
using Duely.Infrastructure.Api.Http.Users.Services.RefreshToken;
using Duely.Infrastructure.Api.Http.Users.Services.WebSockets;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Api.Http.Users;

[ApiController]
[Route("users")]
internal sealed class UsersController(
    IMediator mediator,
    IAuthTokenService authTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IIdentityTicketService identityTicketService,
    IOptions<IdentityTicketOptions> identityTicketOptions,
    IUserContext userContext,
    IUserWebSocketHandler webSocketHandler)
    : ControllerBase
{
    private const string RefreshTokenCookieKey = "refresh_token";
    private const string IdentityTicketCookieKey = "identity_ticket";
    
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
        
        var cookieExpirationDate = DateTimeOffset.UtcNow.AddDays(refreshTokenOptions.Value.ExpiresDays);
        AppendCookie(RefreshTokenCookieKey, refreshToken, cookieExpirationDate);
        
        var authToken = authTokenService.GenerateAuthToken(result.Value);
        return authToken;
    }

    [HttpGet("iam")]
    [Authorize]
    public async Task<ActionResult<UserDto>> IamAsync(CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery
        {
            Id = userContext.UserId
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetByIdAsync(
        [FromQuery] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery
        {
            Id = id
        };

        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetByNicknameAsync(
        [FromQuery] string nickname,
        CancellationToken cancellationToken)
    {
        var query = new GetUserByNicknameQuery
        {
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
        
        var cookieExpirationDate = DateTimeOffset.UtcNow.AddDays(refreshTokenOptions.Value.ExpiresDays);
        AppendCookie(RefreshTokenCookieKey, newRefreshToken, cookieExpirationDate);
        
        var authToken = authTokenService.GenerateAuthToken(result.Value);
        return authToken;
    }

    [HttpPost("identity-ticket")]
    [Authorize]
    public async Task<IActionResult> CreateIdentityTicketAsync(CancellationToken cancellationToken)
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

        var cookieExpirationDate = DateTimeOffset.UtcNow.AddMinutes(identityTicketOptions.Value.ExpiresMinutes);
        AppendCookie(IdentityTicketCookieKey, identityTicket, cookieExpirationDate);

        return this.HandleResult(Result.Ok());
    }

    [HttpGet("connect")]
    public async Task<IActionResult> ConnectAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(IdentityTicketCookieKey, out var identityTicket))
        {
            return this.HandleResult(new ForbiddenError());
        }
        
        var command = new UseIdentityTicketCommand
        {
            IdentityTicket = identityTicket
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

    private void AppendCookie(string key, string value, DateTimeOffset expirationDate)
    {
        Response.Cookies.Append(key, value, new CookieOptions
        {
            Expires = expirationDate,
            HttpOnly = true, // Prevents JavaScript access (XSS protection)
            Secure = true, // Forces HTTPS transmission
            SameSite = SameSiteMode.Strict // Prevents CSRF attacks
        });
    }
}
