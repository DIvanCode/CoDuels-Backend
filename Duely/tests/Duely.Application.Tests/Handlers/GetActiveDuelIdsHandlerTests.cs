using System.Text.Json;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models.Duels;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public sealed class GetActiveDuelIdsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_all_current_user_duels_newest_first_and_excludes_finished_or_other_users()
    {
        var user = EntityFactory.MakeUser(1, "user");
        var opponent1 = EntityFactory.MakeUser(2, "opponent1");
        var opponent2 = EntityFactory.MakeUser(3, "opponent2");
        var outsider = EntityFactory.MakeUser(4, "outsider");
        var start = DateTime.UtcNow;
        var finished = EntityFactory.MakeDuel(14, user, outsider, start: start.AddMinutes(1));
        finished.Status = DuelStatus.Finished;
        Context.Duels.AddRange(
            EntityFactory.MakeDuel(10, user, opponent1, start: start.AddMinutes(-1)),
            EntityFactory.MakeDuel(11, user, opponent2, start: start),
            EntityFactory.MakeDuel(12, outsider, user, start: start),
            EntityFactory.MakeDuel(13, opponent1, outsider, start: start.AddMinutes(1)),
            finished);
        await Context.SaveChangesAsync();

        var result = await new GetActiveDuelIdsHandler(Context).Handle(
            new GetActiveDuelIdsQuery { UserId = user.Id }, CancellationToken.None);

        result.DuelIds.Should().Equal(12, 11, 10);
        JsonSerializer.Serialize(result).Should().Be("{\"duel_ids\":[12,11,10]}");
    }

    [Fact]
    public async Task Returns_empty_list_when_no_active_duels()
    {
        var result = await new GetActiveDuelIdsHandler(Context).Handle(
            new GetActiveDuelIdsQuery { UserId = 1 }, CancellationToken.None);

        result.DuelIds.Should().BeEmpty();
        JsonSerializer.Serialize(result).Should().Be("{\"duel_ids\":[]}");
    }
}
