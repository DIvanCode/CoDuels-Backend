using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Duely.Application.Services.Errors;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class UpdateDuelTaskSolutionHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Updates_solution_for_user1()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        duel.User1Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        duel.User2Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = u1.Id,
            DuelId = duel.Id,
            TaskKey = 'A',
            Solution = "print(1)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Duels.AsNoTracking().SingleAsync(d => d.Id == duel.Id);
        stored.User1Solutions['A'].Solution.Should().Be("print(1)");
        stored.User1Solutions['A'].Language.Should().Be(Language.Python);
    }

    [Fact]
    public async Task Updates_solution_for_user2()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        duel.User1Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        duel.User2Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = u2.Id,
            DuelId = duel.Id,
            TaskKey = 'A',
            Solution = "print(2)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Duels.AsNoTracking().SingleAsync(d => d.Id == duel.Id);
        stored.User2Solutions['A'].Solution.Should().Be("print(2)");
        stored.User2Solutions['A'].Language.Should().Be(Language.Python);
    }

    [Fact]
    public async Task Creates_outbox_message_when_opponent_code_visible()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        duel.Configuration.ShouldShowOpponentSolution = true;
        duel.User1Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        duel.User2Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = u1.Id,
            DuelId = duel.Id,
            TaskKey = 'A',
            Solution = "print(1)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var outboxMessage = Context.OutboxMessages.Single(m => m.Type == OutboxType.SendMessage);
        var payload = (SendMessagePayload)outboxMessage.Payload;
        payload.UserId.Should().Be(u2.Id);
        payload.Message.Should().BeOfType<OpponentSolutionUpdatedMessage>();
        var codeMessage = (OpponentSolutionUpdatedMessage)payload.Message;
        codeMessage.DuelId.Should().Be(duel.Id);
        codeMessage.TaskKey.Should().Be("A");
        codeMessage.Solution.Should().Be("print(1)");
        codeMessage.Language.Should().Be(Language.Python);
    }

    [Fact]
    public async Task Does_not_create_outbox_message_when_opponent_code_hidden()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        duel.Configuration.ShouldShowOpponentSolution = false;
        duel.User1Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        duel.User2Solutions = new Dictionary<char, DuelTaskSolution>
        {
            ['A'] = new DuelTaskSolution
            {
                Solution = string.Empty,
                Language = Language.Python
            }
        };
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = u1.Id,
            DuelId = duel.Id,
            TaskKey = 'A',
            Solution = "print(1)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_forbidden_when_user_not_in_duel()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = 99,
            DuelId = duel.Id,
            TaskKey = 'A',
            Solution = "print(3)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_not_found_when_duel_missing()
    {
        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = 1,
            DuelId = 999,
            TaskKey = 'A',
            Solution = "print(4)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_not_found_when_task_key_missing()
    {
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(u1, u2);
        await Context.SaveChangesAsync();

        var duel = EntityFactory.MakeDuel(1, u1, u2);
        Context.Duels.Add(duel);
        await Context.SaveChangesAsync();

        var handler = new UpdateDuelTaskSolutionHandler(Context);
        var res = await handler.Handle(new UpdateDuelTaskSolutionCommand
        {
            UserId = u1.Id,
            DuelId = duel.Id,
            TaskKey = 'Z',
            Solution = "print(5)",
            Language = Language.Python
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}

