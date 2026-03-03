using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.CodeRuns;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public class GetCodeRunHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_run_absent()
    {
        var ctx = Context;

        var handler = new GetCodeRunHandler(ctx);
        var res = await handler.Handle(new GetCodeRunQuery
        {
            UserId = 1,
            Id = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Forbidden_when_user_not_owner()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var run = MakeCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Done);
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new GetCodeRunHandler(ctx);
        var res = await handler.Handle(new GetCodeRunQuery
        {
            UserId = 2,
            Id = 100
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_run_for_owner()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Done, "print(42)", Language.Python, "input", "output");
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new GetCodeRunHandler(ctx);
        var res = await handler.Handle(new GetCodeRunQuery
        {
            UserId = 1,
            Id = 100
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(100);
        res.Value.Code.Should().Be("print(42)");
        res.Value.Language.Should().Be(Language.Python);
        res.Value.Input.Should().Be("input");
        res.Value.Status.Should().Be(UserCodeRunStatus.Done);
        res.Value.Output.Should().Be("output");
        res.Value.Error.Should().BeNull();
    }

    [Fact]
    public async Task Returns_run_with_error()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        var run = MakeCodeRun(100, u1, "exec-id-1", UserCodeRunStatus.Done, "print(42)", Language.Python, "input", null, "Runtime error");
        ctx.CodeRuns.Add(run);
        await ctx.SaveChangesAsync();

        var handler = new GetCodeRunHandler(ctx);
        var res = await handler.Handle(new GetCodeRunQuery
        {
            UserId = 1,
            Id = 100
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(100);
        res.Value.Error.Should().Be("Runtime error");
        res.Value.Output.Should().BeNull();
    }

    private static CodeRun MakeCodeRun(int id, User user, string executionId, UserCodeRunStatus status,
        string code = "print(1)", Language language = Language.Python, string input = "test", 
        string? output = null, string? error = null)
    {
        return new CodeRun
        {
            Id = id,
            User = user,
            Code = code,
            Language = language,
            Input = input,
            Status = status,
            Output = output,
            Error = error,
            ExecutionId = executionId,
            CreatedAt = DateTime.UtcNow
        };
    }
}