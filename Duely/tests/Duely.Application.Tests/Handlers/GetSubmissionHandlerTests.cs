using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

public class GetSubmissionHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_submission_for_user_in_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        var sub = EntityFactory.MakeSubmission(100, duel, u1, status: SubmissionStatus.Done, verdict: "Accepted");
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);
        var res = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 100,
            UserId = 1,
            DuelId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.SubmissionId.Should().Be(100);
        res.Value.Verdict.Should().Be("Accepted");
        res.Value.Solution.Should().Be(sub.Code);
    }

    [Fact]
    public async Task Hides_solution_and_message_for_non_owner()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        var sub = EntityFactory.MakeSubmission(100, duel, u2, message: "details");
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);
        var res = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 100, UserId = 1, DuelId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Solution.Should().BeEmpty();
        res.Value.Message.Should().BeNull();
    }

    [Fact]
    public async Task Returns_submission_for_non_participant_without_solution()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var viewer = EntityFactory.MakeUser(3, "viewer");
        ctx.Users.AddRange(u1, u2, viewer);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        var sub = EntityFactory.MakeSubmission(100, duel, u1, message: "details");
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);
        var res = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 100,
            UserId = viewer.Id,
            DuelId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Solution.Should().BeEmpty();
        res.Value.Message.Should().BeNull();
    }
}
