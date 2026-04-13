using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.UserActions;
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
}
