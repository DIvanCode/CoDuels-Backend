using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Domain.Models.Duels.Pending;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class CancelDuelSearchHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Cancel_search_removes_only_ranked_rows()
    {
        var user = EntityFactory.MakeUser(1, "user");
        var opponent = EntityFactory.MakeUser(2, "opponent");
        Context.Users.AddRange(user, opponent);
        Context.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user,
                Rating = user.Rating,
                CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = user,
                User2 = opponent,
                Configuration = null,
                IsAccepted = true,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var handler = new CancelDuelSearchHandler(Context);
        var result = await handler.Handle(new CancelDuelSearchCommand { UserId = user.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await Context.PendingDuels.OfType<RankedPendingDuel>().CountAsync()).Should().Be(0);
        (await Context.PendingDuels.OfType<FriendlyPendingDuel>().SingleAsync()).IsAccepted.Should().BeTrue();
        (await Context.OutboxMessages.CountAsync()).Should().Be(0);

        (await handler.Handle(new CancelDuelSearchCommand { UserId = user.Id }, CancellationToken.None))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_search_rejects_unknown_user()
    {
        var result = await new CancelDuelSearchHandler(Context).Handle(
            new CancelDuelSearchCommand { UserId = 999 }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(error => error is EntityNotFoundError);
    }
}
