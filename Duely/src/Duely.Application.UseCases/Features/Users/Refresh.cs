using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Users;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RefreshCommand : IRequest<Result<TokenDto>>
{
    public required string RefreshToken { get; init; }
}

public sealed class RefreshHandler(Context context, ITokenService tokenService, ILogger<RefreshHandler> logger)
    : IRequestHandler<RefreshCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.RefreshToken == command.RefreshToken, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Refresh failed: invalid refresh token");
            return new EntityNotFoundError(nameof(User), nameof(User.RefreshToken), "***");
        }

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user);
        user.RefreshToken = refreshToken;

        logger.LogInformation("Refresh success. UserId={UserId}", user.Id);

        await context.SaveChangesAsync(cancellationToken);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
