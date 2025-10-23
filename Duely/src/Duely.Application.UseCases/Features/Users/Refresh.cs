using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Users;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

public sealed class RefreshCommand : IRequest<Result<TokenDto>>
{
    public required int UserId { get; init; }
    public required string RefreshToken { get; init; }
}

public sealed class RefreshHandler(Context context, ITokenService tokenService)
    : IRequestHandler<RefreshCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }

        if (user.RefreshToken != command.RefreshToken)
        {
            return new AuthenticationError();
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
