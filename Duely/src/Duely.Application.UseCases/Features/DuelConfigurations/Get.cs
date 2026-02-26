using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class GetDuelConfigurationQuery : IRequest<Result<DuelConfigurationDto>>
{
    public required int Id { get; init; }
}

public sealed class GetDuelConfigurationHandler(Context context)
    : IRequestHandler<GetDuelConfigurationQuery, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        GetDuelConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
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
        };
    }
}

