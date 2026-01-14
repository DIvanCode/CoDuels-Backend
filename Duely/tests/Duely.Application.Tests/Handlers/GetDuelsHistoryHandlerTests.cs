using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Moq;
using Xunit;

public class GetDuelsHistoryHandlerTests : ContextBasedTest
{
    private readonly Mock<IRatingManager> _ratingManagerMock = new();
    private readonly GetDuelsHistoryHandler _sut;
    
    public GetDuelsHistoryHandlerTests()
    {
        _sut = new GetDuelsHistoryHandler(Context, _ratingManagerMock.Object, new TaskService());
    }
    
    [Fact]
    public async Task NotFound_when_user_does_not_exist()
    {
        var ctx = Context;

        var res = await _sut.Handle(
            new GetDuelsHistoryQuery { UserId = 42 },
            CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_empty_list_when_user_has_no_duels()
    {
        var ctx = Context;

        var user = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var res = await _sut.Handle(
            new GetDuelsHistoryQuery { UserId = 1 },
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_sorted_history_with_correct_opponent_and_winner_for_user1()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        ctx.Users.AddRange(u1, u2, u3);
        var duel1 = EntityFactory.MakeDuel(10, u1, u2, "TASK1");
        duel1.Status = DuelStatus.Finished;
        duel1.EndTime = DateTime.UtcNow.AddMinutes(-3);
        duel1.Winner = u1;
        duel1.User1FinalRating = duel1.User1InitRating + 1;
        duel1.User2FinalRating = duel1.User2InitRating - 1;
        var duel2 = EntityFactory.MakeDuel(11, u1, u2, "TASK2");
        duel2.Status = DuelStatus.Finished;
        duel2.EndTime = DateTime.UtcNow.AddMinutes(-2);
        duel2.Winner = u2;
        duel2.User1FinalRating = duel2.User1InitRating - 1;
        duel2.User2FinalRating = duel2.User2InitRating + 1;
        var duelOther = EntityFactory.MakeDuel(12, u2, u3, "TASK3");
        duelOther.Status = DuelStatus.InProgress;

        ctx.Duels.AddRange(duel1, duel2, duelOther);
        await ctx.SaveChangesAsync();

        var res = await _sut.Handle(
            new GetDuelsHistoryQuery { UserId = 1 },
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var history = res.Value;
        history.Should().HaveCount(2);
        history.Select(h => h.Id).Should().ContainInOrder(11, 10);

        var first = history[0];  
        var second = history[1];

        first.Participants.Length.Should().Be(2);
        first.Participants.Should().Contain(p => p.Id == u1.Id && p.Rating == duel1.User1InitRating);
        first.Participants.Should().Contain(p => p.Id == u2.Id && p.Rating == duel2.User1InitRating);
        first.Status.Should().Be(DuelStatus.Finished);
        first.WinnerId.Should().Be(2);
        
        second.Participants.Length.Should().Be(2);
        second.Participants.Should().Contain(p => p.Id == u1.Id && p.Rating == duel1.User1InitRating);
        second.Participants.Should().Contain(p => p.Id == u2.Id && p.Rating == duel2.User1InitRating);
        second.Status.Should().Be(DuelStatus.Finished);
        second.WinnerId.Should().Be(1);
    }

    [Fact]
    public async Task Opponent_and_winner_are_correct_for_user2_side()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var duel = EntityFactory.MakeDuel(20, u1, u2, "TASK");
        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        duel.Winner = u1;
        duel.User1FinalRating = duel.User1InitRating + 1;
        duel.User2FinalRating = duel.User2InitRating - 1;

        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var res = await _sut.Handle(
            new GetDuelsHistoryQuery { UserId = 2 },
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var item = res.Value.Single();
        item.Participants.Length.Should().Be(2);
        item.Participants.Should().Contain(p => p.Id == u1.Id && p.Rating == duel.User1InitRating);
        item.Participants.Should().Contain(p => p.Id == u2.Id && p.Rating == duel.User1InitRating);
        item.WinnerId.Should().Be(1);
    }
}
