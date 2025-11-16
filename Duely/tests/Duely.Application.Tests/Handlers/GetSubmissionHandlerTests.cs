using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
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
    }

    [Fact]
    public async Task NotFound_when_not_matches_user_or_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        var sub = EntityFactory.MakeSubmission(100, duel, u2);
        ctx.Submissions.Add(sub);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);
        var res = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 100, UserId = 1, DuelId = 10
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
