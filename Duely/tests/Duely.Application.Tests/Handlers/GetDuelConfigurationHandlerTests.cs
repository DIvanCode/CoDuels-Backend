using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class GetDuelConfigurationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_configuration_when_found()
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

        var handler = new GetDuelConfigurationHandler(Context);
        var res = await handler.Handle(new GetDuelConfigurationQuery(config.Id), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(config.Id);
        res.Value.Tasks.Should().ContainKey('A');
        res.Value.Tasks['A'].Level.Should().Be(1);
        res.Value.Tasks['A'].Topics.Should().ContainSingle().Which.Should().Be("basics");
    }

    [Fact]
    public async Task Returns_not_found_when_absent()
    {
        var handler = new GetDuelConfigurationHandler(Context);
        var res = await handler.Handle(new GetDuelConfigurationQuery(999), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
