using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class UpdateDuelConfigurationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Updates_configuration_when_found()
    {
        var config = new DuelConfiguration
        {
            ShouldShowOpponentCode = false,
            MaxDurationMinutes = 30,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = ["basics"]
                }
            }
        };
        Context.DuelConfigurations.Add(config);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelConfigurationHandler(Context);
        var res = await handler.Handle(new UpdateDuelConfigurationCommand
        {
            Id = config.Id,
            ShouldShowOpponentCode = true,
            MaxDurationMinutes = 60,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Parallel,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 2,
                    Topics = ["arrays"]
                },
                ['B'] = new()
                {
                    Level = 3,
                    Topics = ["graphs"]
                }
            }
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.TasksOrder.Should().Be(DuelTasksOrder.Parallel);
        res.Value.Tasks.Should().ContainKeys('A', 'B');

        var entity = await Context.DuelConfigurations.AsNoTracking()
            .SingleAsync(c => c.Id == config.Id);
        entity.MaxDurationMinutes.Should().Be(60);
        entity.TasksConfigurations['B'].Level.Should().Be(3);
    }

    [Fact]
    public async Task Returns_not_found_when_absent()
    {
        var handler = new UpdateDuelConfigurationHandler(Context);
        var res = await handler.Handle(new UpdateDuelConfigurationCommand
        {
            Id = 999,
            ShouldShowOpponentCode = true,
            MaxDurationMinutes = 60,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
