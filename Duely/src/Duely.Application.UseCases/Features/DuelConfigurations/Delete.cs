using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed record DeleteDuelConfigurationCommand(int Id) : IRequest<Result>;

public sealed class DeleteDuelConfigurationHandler(Context context)
    : IRequestHandler<DeleteDuelConfigurationCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations.SingleOrDefaultAsync(
            c => c.Id == request.Id,
            cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

        context.DuelConfigurations.Remove(configuration);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

