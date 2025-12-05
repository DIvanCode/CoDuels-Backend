using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.UseCases.Features.RateLimiting;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;


internal sealed class DummySubmissionRateLimiter : ISubmissionRateLimiter
{
    public Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(true);
}

public class SendSubmissionHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_duel_absent()
    {
        var ctx = Context;
        var limiter = new DummySubmissionRateLimiter();

        var handler = new SendSubmissionHandler(ctx, limiter);

        var res = await handler.Handle(new SendSubmissionCommand
        {
            DuelId = 10,
            UserId = 1,
            Code = "print(1)",
            Language = "py"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var limiter = new DummySubmissionRateLimiter();

        var handler = new SendSubmissionHandler(ctx, limiter);

        var res = await handler.Handle(new SendSubmissionCommand
        {
            DuelId = 10,
            UserId = 999,
            Code = "print(1)",
            Language = "py"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_creates_submission_and_outbox_message()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK-10");
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var limiter = new DummySubmissionRateLimiter();

        var handler = new SendSubmissionHandler(ctx, limiter);

        var res = await handler.Handle(new SendSubmissionCommand
        {
            DuelId = 10,
            UserId = 1,
            Code = "print(1)",
            Language = "py"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue($"errors: {string.Join(" | ", res.Errors.Select(e => e.Message))}");

        // Проверяем, что submission создался
        var sub = await ctx.Submissions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Duel)
            .SingleAsync(s => s.Id == res.Value.SubmissionId);

        sub.Status.Should().Be(SubmissionStatus.Queued);
        sub.User!.Id.Should().Be(1);
        sub.Duel!.Id.Should().Be(10);
        sub.Language.Should().Be("py");
        sub.Code.Should().Be("print(1)");

        // Проверяем, что Outbox содержит сообщение TestSolution
        var outboxMsg = await ctx.Outbox.AsNoTracking().SingleAsync();
        outboxMsg.Type.Should().Be(OutboxType.TestSolution);
        outboxMsg.Status.Should().Be(OutboxStatus.ToDo);
        outboxMsg.Payload.Should().Contain("TASK-10");
        outboxMsg.Payload.Should().Contain("print(1)");
        outboxMsg.Payload.Should().Contain("py");
    }
}
