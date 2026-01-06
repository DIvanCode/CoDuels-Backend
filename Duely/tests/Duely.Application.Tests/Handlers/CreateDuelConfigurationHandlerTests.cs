using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class CreateDuelConfigurationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creates_configuration_and_returns_dto()
    {
        var handler = new CreateDuelConfigurationHandler(Context);
        var command = new CreateDuelConfigurationCommand
        {
            ShouldShowOpponentCode = true,
            MaxDurationMinutes = 45,
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
        };

        var res = await handler.Handle(command, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().BeGreaterThan(0);
        res.Value.ShouldShowOpponentCode.Should().BeTrue();
        res.Value.MaxDurationMinutes.Should().Be(45);
        res.Value.TasksCount.Should().Be(2);
        res.Value.TasksOrder.Should().Be(DuelTasksOrder.Parallel);
        res.Value.Tasks.Should().ContainKeys('A', 'B');
        res.Value.Tasks['A'].Level.Should().Be(2);
        res.Value.Tasks['A'].Topics.Should().ContainSingle().Which.Should().Be("arrays");

        var entity = await Context.DuelConfigurations.AsNoTracking()
            .SingleAsync(c => c.Id == res.Value.Id);
        entity.TasksConfigurations['B'].Level.Should().Be(3);
    }
}
