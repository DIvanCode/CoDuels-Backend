using Duely.Application.Services.Errors;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class DeleteDuelConfigurationCommand : IRequest<Result>
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
}

public sealed class DeleteDuelConfigurationHandler(Context context)
    : IRequestHandler<DeleteDuelConfigurationCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations
            .Include(c => c.Owner)
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

        if (configuration.Owner?.Id != request.UserId)
        {
            return new ForbiddenError(nameof(DuelConfiguration), "delete", nameof(DuelConfiguration.Id), request.Id);
        }

        context.DuelConfigurations.Remove(configuration);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

