using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class CreateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required bool ShowOpponentCode { get; init; }
    public required int MaxDurationMinutes { get; init; }
    public required int TasksCount { get; init; }
    public required DuelTasksOrder TasksOrder { get; init; }
    public required List<DuelTaskConfiguration> TasksConfigurations { get; init; }
}

public sealed class CreateDuelConfigurationHandler(Context context)
    : IRequestHandler<CreateDuelConfigurationCommand, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        CreateDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var configuration = new DuelConfiguration
        {
            ShowOpponentCode = request.ShowOpponentCode,
            MaxDurationMinutes = request.MaxDurationMinutes,
            TasksCount = request.TasksCount,
            TasksOrder = request.TasksOrder,
            TasksConfigurations = request.TasksConfigurations
        };

        context.DuelConfigurations.Add(configuration);
        await context.SaveChangesAsync(cancellationToken);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShowOpponentCode = configuration.ShowOpponentCode,
            MaxDurationMinutes = configuration.MaxDurationMinutes,
            TasksCount = configuration.TasksCount,
            TasksOrder = configuration.TasksOrder,
            Tasks = configuration.TasksConfigurations
                .Select(c => new DuelTaskConfigurationDto
                {
                    Order = c.Order,
                    Level = c.Level,
                    Topics = c.Topics
                }).ToList()
        };
    }
}

