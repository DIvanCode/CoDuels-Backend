using Duely.Application.Handlers.Users.Models;
using Duely.Application.Handlers.Users.Validators;
using Duely.Domain.Kernel.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Users.UseCases;

public sealed class ExchangeRefreshTokenCommand : IRequest<Result<UserDto>>
{
    public required string RefreshToken { get; init; }
    public required string NewRefreshToken { get; init; }
}

internal sealed class ExchangeRefreshTokenHandler(Context context, ILogger<ExchangeRefreshTokenHandler> logger)
    : IRequestHandler<ExchangeRefreshTokenCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(ExchangeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Where(u => u.RefreshToken == command.RefreshToken)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var userWithNewRefreshTokenExists = await context.Users
            .Where(u => u.RefreshToken == command.NewRefreshToken)
            .AnyAsync(cancellationToken);
        if (userWithNewRefreshTokenExists)
        {
            return new RefreshTokenAlreadyExistsError();
        }

        user.UpdateRefreshToken(command.NewRefreshToken);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} refreshed token", user.Nickname);

        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            CreatedAt = user.CreatedAt,
            Rating = user.Rating
        };
    }
}

internal sealed class ExchangeRefreshTokenCommandValidator : AbstractValidator<ExchangeRefreshTokenCommand>
{
    public ExchangeRefreshTokenCommandValidator(RefreshTokenValidator refreshTokenValidator)
    {
        RuleFor(x => x.NewRefreshToken).SetValidator(refreshTokenValidator);
    }
}
