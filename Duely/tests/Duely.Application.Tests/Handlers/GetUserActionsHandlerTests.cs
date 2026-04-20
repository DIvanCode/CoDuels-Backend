using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.UserActions;
using Duely.Domain.Models.UserActions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class GetUserActionsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_actions_filtered_by_duel_user_and_task_ordered_by_sequence()
    {
        var now = DateTime.UtcNow;

        var matchingSecond = new WriteCodeUserAction
        {
            EventId = Guid.NewGuid(),
            SequenceId = 2,
            Timestamp = now.AddSeconds(2),
            DuelId = 10,
            TaskKey = 'A',
            UserId = 100,
            CodeLength = 12,
            CursorLine = 3
        };
        var matchingFirst = new RunSampleTestUserAction
        {
            EventId = Guid.NewGuid(),
            SequenceId = 1,
            Timestamp = now.AddSeconds(1),
            DuelId = 10,
            TaskKey = 'A',
            UserId = 100
        };

        Context.UserActions.AddRange(
            matchingSecond,
            matchingFirst,
            new RunSampleTestUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 3,
                Timestamp = now.AddSeconds(3),
                DuelId = 10,
                TaskKey = 'B',
                UserId = 100
            },
            new RunSampleTestUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 4,
                Timestamp = now.AddSeconds(4),
                DuelId = 11,
                TaskKey = 'A',
                UserId = 100
            },
            new RunSampleTestUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 5,
                Timestamp = now.AddSeconds(5),
                DuelId = 10,
                TaskKey = 'A',
                UserId = 101
            });
        await Context.SaveChangesAsync();

        var handler = new GetUserActionsHandler(Context);

        var result = await handler.Handle(new GetUserActionsQuery
        {
            DuelId = 10,
            UserId = 100,
            TaskKey = 'A'
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(action => action.EventId).Should().ContainInOrder(
            matchingFirst.EventId,
            matchingSecond.EventId);
    }

    [Fact]
    public async Task Returns_empty_list_when_no_matching_actions()
    {
        var handler = new GetUserActionsHandler(Context);

        var result = await handler.Handle(new GetUserActionsQuery
        {
            DuelId = 999,
            UserId = 123,
            TaskKey = 'Z'
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();

        var totalCount = await Context.UserActions.CountAsync();
        totalCount.Should().Be(0);
    }
}
