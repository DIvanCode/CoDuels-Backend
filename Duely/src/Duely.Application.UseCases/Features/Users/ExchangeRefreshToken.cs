using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Users.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Users;

public sealed class ExchangeRefreshTokenCommand : IRequest<Result<UserDto>>
{
    public required string RefreshToken { get; init; }
    public required string NewRefreshToken { get; init; }
}

internal sealed class RefreshTokenHandler(Context context, ILogger<RefreshTokenHandler> logger)
    : IRequestHandler<ExchangeRefreshTokenCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(ExchangeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Nickname)
            .Include(u => u.Rating)
            .SingleOrDefaultAsync(u => u.RefreshToken == command.RefreshToken, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var userWithNewRefreshTokenExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.RefreshToken == command.NewRefreshToken, cancellationToken);
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
            Nickname = user.Nickname.Value,
            Rating = user.Rating.Value,
            CreatedAt = user.CreatedAt
        };
    }
}

internal sealed class ExchangeRefreshTokenCommandValidator : AbstractValidator<ExchangeRefreshTokenCommand>
{
    public ExchangeRefreshTokenCommandValidator()
    {
        RuleFor(x => x.NewRefreshToken)
            .MaximumLength(1024).WithMessage("Слишком длинный новый обменный токен.");
    }
}
