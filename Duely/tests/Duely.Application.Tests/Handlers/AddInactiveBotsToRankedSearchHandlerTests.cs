using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Domain.Models.Duels.Pending;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class AddInactiveBotsToRankedSearchHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Adds_ranked_search_for_bot_already_in_active_duels_and_is_idempotent()
    {
        var bot = EntityFactory.MakeBot(1);
        var opponent1 = EntityFactory.MakeUser(2, "opponent1");
        var opponent2 = EntityFactory.MakeUser(3, "opponent2");
        Context.Duels.AddRange(
            EntityFactory.MakeDuel(10, bot, opponent1),
            EntityFactory.MakeDuel(11, opponent2, bot));
        await Context.SaveChangesAsync();

        var handler = new AddInactiveBotsToRankedSearchHandler(Context);
        (await handler.Handle(new AddInactiveBotsToRankedSearchCommand(), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await handler.Handle(new AddInactiveBotsToRankedSearchCommand(), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var pending = await Context.PendingDuels.OfType<RankedPendingDuel>()
            .Include(d => d.User)
            .SingleAsync();
        pending.User.Id.Should().Be(bot.Id);
    }
}
