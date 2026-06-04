using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class DeleteDuelConfigurationCommand : IRequest<Result>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
}

internal sealed class DeleteDuelConfigurationHandler(
    Context context,
    ILogger<DeleteDuelConfigurationHandler> logger)
    : IRequestHandler<DeleteDuelConfigurationCommand, Result>
{
    public async Task<Result> Handle(DeleteDuelConfigurationCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var configuration = await context.DuelConfigurations
            .Include(c => c.CreatedBy)
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (configuration is null)
        {
            return new DuelConfigurationNotFoundError();
        }

        if (configuration.CreatedBy is not null && configuration.CreatedBy.Id != command.UserId)
        {
            return new ForbiddenError();
        }

        context.DuelConfigurations.Remove(configuration);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} deleted duel configuration {Id}", user.Nickname, configuration.Id);

        return Result.Ok();
    }
}
