using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

public class GetUserSubmissionsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_not_part_of_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        ctx.Users.AddRange(u1, u2, u3);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var handler = new GetUserSubmissionsHandler(ctx);
        var res = await handler.Handle(new GetUserSubmissionsQuery { UserId = 3, DuelId = 10 }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_sorted_list_for_participant()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);
        ctx.Submissions.Add(EntityFactory.MakeSubmission(1, duel, u1, time: System.DateTime.UtcNow.AddMinutes(1), status: SubmissionStatus.Running));
        ctx.Submissions.Add(EntityFactory.MakeSubmission(2, duel, u1, time: System.DateTime.UtcNow.AddMinutes(2), status: SubmissionStatus.Done, verdict: "Accepted"));
        ctx.Submissions.Add(EntityFactory.MakeSubmission(3, duel, u2, time: System.DateTime.UtcNow.AddMinutes(3)));
        await ctx.SaveChangesAsync();

        var handler = new GetUserSubmissionsHandler(ctx);
        var res = await handler.Handle(new GetUserSubmissionsQuery { UserId = 1, DuelId = 10 }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Select(i => i.SubmissionId).Should().ContainInOrder(1, 2);
        res.Value.Last().Verdict.Should().Be("Accepted");
    }
}
