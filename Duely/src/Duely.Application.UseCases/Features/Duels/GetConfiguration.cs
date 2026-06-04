using Duely.Application.UseCases.Dto.Duels;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelConfigurationQuery : IRequest<Result<DuelConfigurationDto>>
{
    public required Guid Id { get; init; }
}

internal sealed class GetDuelConfigurationHandler(Context context)
    : IRequestHandler<GetDuelConfigurationQuery, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        GetDuelConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations
            .SingleOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        if (configuration is null)
        {
            return new DuelConfigurationNotFoundError();
        }

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
            DurationMinutes = configuration.DurationMinutes,
            ProblemsCount = configuration.ProblemsCount,
            ProblemsOrder = configuration.ProblemsOrder
        };
    }
}
