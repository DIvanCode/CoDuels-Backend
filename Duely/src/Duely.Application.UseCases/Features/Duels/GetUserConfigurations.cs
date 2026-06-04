using Duely.Application.UseCases.Dto.Duels;
using Duely.Domain.Common.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetUserDuelConfigurationsQuery : IRequest<Result<List<DuelConfigurationDto>>>
{
    public required Guid UserId { get; init; }
}

internal sealed class GetUserDuelConfigurationsHandler(Context context)
    : IRequestHandler<GetUserDuelConfigurationsQuery, Result<List<DuelConfigurationDto>>>
{
    public async Task<Result<List<DuelConfigurationDto>>> Handle(
        GetUserDuelConfigurationsQuery query,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var configurations = await context.DuelConfigurations
            .Where(configuration => configuration.CreatedBy != null && configuration.CreatedBy.Id == query.UserId)
            .OrderBy(configuration => configuration.Id)
            .ToListAsync(cancellationToken);

        return configurations
            .Select(configuration => new DuelConfigurationDto
            {
                Id = configuration.Id,
                ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
                DurationMinutes = configuration.DurationMinutes,
                ProblemsCount = configuration.ProblemsCount,
                ProblemsOrder = configuration.ProblemsOrder
            })
            .ToList();
    }
}
