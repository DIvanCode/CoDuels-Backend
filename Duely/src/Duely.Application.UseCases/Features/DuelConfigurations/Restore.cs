using Duely.Application.Services.Errors;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class RestoreDuelConfigurationCommand : IRequest<Result>
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
}

public sealed class RestoreDuelConfigurationHandler(Context context)
    : IRequestHandler<RestoreDuelConfigurationCommand, Result>
{
    public async Task<Result> Handle(
        RestoreDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations
            .Include(c => c.Owner)
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

        if (configuration.Owner?.Id != request.UserId || !configuration.IsDeleted)
        {
            return new ForbiddenError(nameof(DuelConfiguration), "restore", nameof(DuelConfiguration.Id), request.Id);
        }

        configuration.IsDeleted = false;
        
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}