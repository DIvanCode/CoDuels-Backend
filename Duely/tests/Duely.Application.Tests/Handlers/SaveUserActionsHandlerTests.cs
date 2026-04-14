using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.UserActions;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.UserActions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class SaveUserActionsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Saves_all_actions_when_user_ids_match_command_user_id()
    {
        var handler = new SaveUserActionsHandler(Context);
        var timestamp = DateTime.UtcNow;
        var user1 = EntityFactory.MakeUser(10, "u1");
        var user2 = EntityFactory.MakeUser(11, "u2");
        var duel = EntityFactory.MakeDuel(36, user1, user2, "TASK-1");
        Context.AddRange(user1, user2, duel);
        await Context.SaveChangesAsync();

        var actions = new List<UserAction>
        {
            new ChooseLanguageUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 1,
                Timestamp = timestamp,
                DuelId = 36,
                TaskKey = 'A',
                UserId = 10,
                Language = Language.Python
            },
            new MoveCursorUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 2,
                Timestamp = timestamp.AddSeconds(1),
                DuelId = 36,
                TaskKey = 'A',
                UserId = 10,
                CodeLength = 5,
                CursorLine = 5,
                PreviousCursorLine = 4
            }
        };

        var result = await handler.Handle(new SaveUserActionsCommand
        {
            UserId = 10,
            Actions = actions
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var savedActions = await Context.UserActions.AsNoTracking().ToListAsync();
        savedActions.Should().HaveCount(2);
        savedActions.Should().Contain(a => a.EventId == actions[0].EventId && a.UserId == 10);
        savedActions.Should().Contain(a => a.EventId == actions[1].EventId && a.UserId == 10);
    }

    [Fact]
    public async Task Returns_forbidden_and_does_not_save_when_any_action_has_other_user_id()
    {
        var handler = new SaveUserActionsHandler(Context);

        var actions = new List<UserAction>
        {
            new SubmitSolutionUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 1,
                Timestamp = DateTime.UtcNow,
                DuelId = 36,
                TaskKey = 'A',
                UserId = 10
            },
            new RunSampleTestUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 2,
                Timestamp = DateTime.UtcNow.AddSeconds(1),
                DuelId = 36,
                TaskKey = 'A',
                UserId = 11
            }
        };

        var result = await handler.Handle(new SaveUserActionsCommand
        {
            UserId = 10,
            Actions = actions
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);

        var savedCount = await Context.UserActions.CountAsync();
        savedCount.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_save_actions_for_finished_duel()
    {
        var handler = new SaveUserActionsHandler(Context);

        var user1 = EntityFactory.MakeUser(20, "u1");
        var user2 = EntityFactory.MakeUser(21, "u2");
        var duel = EntityFactory.MakeDuel(77, user1, user2, "TASK-77");
        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        Context.AddRange(user1, user2, duel);
        await Context.SaveChangesAsync();

        var result = await handler.Handle(new SaveUserActionsCommand
        {
            UserId = 20,
            Actions =
            [
                new SubmitSolutionUserAction
                {
                    EventId = Guid.NewGuid(),
                    SequenceId = 1,
                    Timestamp = DateTime.UtcNow,
                    DuelId = 77,
                    TaskKey = 'A',
                    UserId = 20
                }
            ]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var savedActions = await Context.UserActions.AsNoTracking().ToListAsync();
        savedActions.Should().BeEmpty();
    }

    [Fact]
    public async Task Saves_only_actions_for_non_finished_duels()
    {
        var handler = new SaveUserActionsHandler(Context);

        var user1 = EntityFactory.MakeUser(30, "u1");
        var user2 = EntityFactory.MakeUser(31, "u2");
        var activeDuel = EntityFactory.MakeDuel(88, user1, user2, "TASK-88");
        var finishedDuel = EntityFactory.MakeDuel(89, user1, user2, "TASK-89");
        finishedDuel.Status = DuelStatus.Finished;
        finishedDuel.EndTime = DateTime.UtcNow;

        Context.AddRange(user1, user2, activeDuel, finishedDuel);
        await Context.SaveChangesAsync();

        var activeActionEventId = Guid.NewGuid();
        var finishedActionEventId = Guid.NewGuid();

        var result = await handler.Handle(new SaveUserActionsCommand
        {
            UserId = 30,
            Actions =
            [
                new RunSampleTestUserAction
                {
                    EventId = activeActionEventId,
                    SequenceId = 1,
                    Timestamp = DateTime.UtcNow,
                    DuelId = 88,
                    TaskKey = 'A',
                    UserId = 30
                },
                new RunSampleTestUserAction
                {
                    EventId = finishedActionEventId,
                    SequenceId = 2,
                    Timestamp = DateTime.UtcNow.AddSeconds(1),
                    DuelId = 89,
                    TaskKey = 'A',
                    UserId = 30
                }
            ]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var savedActions = await Context.UserActions.AsNoTracking().ToListAsync();
        savedActions.Should().HaveCount(1);
        savedActions.Should().Contain(action => action.EventId == activeActionEventId);
        savedActions.Should().NotContain(action => action.EventId == finishedActionEventId);
    }
}
