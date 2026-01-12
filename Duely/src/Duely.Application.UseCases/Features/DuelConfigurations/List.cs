using Duely.Application.UseCases.Dtos;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class GetUserDuelConfigurationsQuery : IRequest<Result<List<DuelConfigurationDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetUserDuelConfigurationsHandler(Context context)
    : IRequestHandler<GetUserDuelConfigurationsQuery, Result<List<DuelConfigurationDto>>>
{
    public async Task<Result<List<DuelConfigurationDto>>> Handle(
        GetUserDuelConfigurationsQuery query,
        CancellationToken cancellationToken)
    {
        var configurations = await context.DuelConfigurations
            .Where(configuration => configuration.Owner != null && configuration.Owner.Id == query.UserId)
            .OrderBy(configuration => configuration.Id)
            .ToListAsync(cancellationToken);

        var result = configurations
            .Select(configuration => new DuelConfigurationDto
            {
                Id = configuration.Id,
                ShouldShowOpponentCode = configuration.ShouldShowOpponentCode,
                MaxDurationMinutes = configuration.MaxDurationMinutes,
                TasksCount = configuration.TasksCount,
                TasksOrder = configuration.TasksOrder,
                Tasks = configuration.TasksConfigurations.ToDictionary(
                    task => task.Key,
                    task => new DuelTaskConfigurationDto
                    {
                        Level = task.Value.Level,
                        Topics = task.Value.Topics
                    })
            })
            .ToList();

        return Result.Ok(result);
    }
}
