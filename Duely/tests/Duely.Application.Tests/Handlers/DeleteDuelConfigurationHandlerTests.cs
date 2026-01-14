using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class DeleteDuelConfigurationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Deletes_configuration_when_owner()
    {
        var owner = EntityFactory.MakeUser(1, "owner");
        Context.Users.Add(owner);

        var config = new DuelConfiguration
        {
            Owner = owner,
            ShouldShowOpponentCode = false,
            MaxDurationMinutes = 30,
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
        };
        Context.DuelConfigurations.Add(config);
        await Context.SaveChangesAsync();

        var handler = new DeleteDuelConfigurationHandler(Context);
        var res = await handler.Handle(new DeleteDuelConfigurationCommand(config.Id, owner.Id), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await Context.DuelConfigurations.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Forbidden_when_not_owner()
    {
        var owner = EntityFactory.MakeUser(1, "owner");
        var other = EntityFactory.MakeUser(2, "other");
        Context.Users.AddRange(owner, other);

        var config = new DuelConfiguration
        {
            Owner = owner,
            ShouldShowOpponentCode = false,
            MaxDurationMinutes = 30,
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
        };
        Context.DuelConfigurations.Add(config);
        await Context.SaveChangesAsync();

        var handler = new DeleteDuelConfigurationHandler(Context);
        var res = await handler.Handle(new DeleteDuelConfigurationCommand(config.Id, other.Id), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Forbidden_when_owner_is_null()
    {
        var config = new DuelConfiguration
        {
            Owner = null,
            ShouldShowOpponentCode = false,
            MaxDurationMinutes = 30,
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
        };
        Context.DuelConfigurations.Add(config);
        await Context.SaveChangesAsync();

        var handler = new DeleteDuelConfigurationHandler(Context);
        var res = await handler.Handle(new DeleteDuelConfigurationCommand(config.Id, 1), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Not_found_when_missing()
    {
        var handler = new DeleteDuelConfigurationHandler(Context);
        var res = await handler.Handle(new DeleteDuelConfigurationCommand(999, 1), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
