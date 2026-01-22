using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Services.Users;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RefreshTokenCommand : IRequest<Result<TokenDto>>
{
    public required string RefreshToken { get; init; }
}

public sealed class RefreshTokenHandler(Context context, ITokenService tokenService, ILogger<RefreshTokenHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.RefreshToken == command.RefreshToken, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.RefreshToken), "***");
        }

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user);
        user.RefreshToken = refreshToken;

        await context.SaveChangesAsync(cancellationToken);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
