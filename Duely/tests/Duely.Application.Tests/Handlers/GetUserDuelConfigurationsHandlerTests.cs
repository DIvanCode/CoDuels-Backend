using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class GetUserDuelConfigurationsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_only_configurations_for_target_user()
    {
        var owner = EntityFactory.MakeUser(1, "owner");
        var otherUser = EntityFactory.MakeUser(2, "other");
        Context.Users.AddRange(owner, otherUser);

        var ownedConfiguration = new DuelConfiguration
        {
            Owner = owner,
            ShouldShowOpponentSolution = false,
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

        var otherConfiguration = new DuelConfiguration
        {
            Owner = otherUser,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 45,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Parallel,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['B'] = new()
                {
                    Level = 2,
                    Topics = ["graphs"]
                }
            }
        };

        Context.DuelConfigurations.AddRange(ownedConfiguration, otherConfiguration);
        await Context.SaveChangesAsync();

        var handler = new GetUserDuelConfigurationsHandler(Context);
        var result = await handler.Handle(
            new GetUserDuelConfigurationsQuery { UserId = owner.Id },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(configuration => configuration.Id == ownedConfiguration.Id);
        var returned = result.Value.Single();
        returned.Tasks.Should().ContainKey('A');
        returned.Tasks['A'].Level.Should().Be(1);
        returned.Tasks['A'].Topics.Should().ContainSingle().Which.Should().Be("basics");
    }

    [Fact]
    public async Task Returns_empty_list_when_user_has_no_configurations()
    {
        var owner = EntityFactory.MakeUser(1, "owner");
        Context.Users.Add(owner);
        await Context.SaveChangesAsync();

        var handler = new GetUserDuelConfigurationsHandler(Context);
        var result = await handler.Handle(
            new GetUserDuelConfigurationsQuery { UserId = owner.Id },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
