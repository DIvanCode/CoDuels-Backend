using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentResults;

public class SendSubmissionHandlerTests
{
    [Fact]
    public async Task NotFound_when_duel_absent()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        var taski = new Mock<ITaskiClient>(MockBehavior.Strict);
        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var res = await handler.Handle(new SendSubmissionCommand {
            DuelId = 10, UserId = 1, Code = "print(1)", Language = "py" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>(MockBehavior.Strict);
        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var res = await handler.Handle(new SendSubmissionCommand {
            DuelId = 10, UserId = 999, Code = "print(1)", Language = "py" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_creates_submission_and_calls_taski()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>();
        taski.Setup(t => t.TestSolutionAsync("TASK-10", It.IsAny<string>(), "print(1)", "py", It.IsAny<CancellationToken>()))
             .ReturnsAsync(Result.Ok());

        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var res = await handler.Handle(new SendSubmissionCommand {
            DuelId = 10, UserId = 1, Code = "print(1)", Language = "py" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var sub = await ctx.Submissions.AsNoTracking().SingleAsync(s => s.Id == res.Value.SubmissionId);
        sub.Status.Should().Be(SubmissionStatus.Queued);
        sub.User.Id.Should().Be(1);
        sub.Duel.Id.Should().Be(10);

        taski.VerifyAll();
    }
}
