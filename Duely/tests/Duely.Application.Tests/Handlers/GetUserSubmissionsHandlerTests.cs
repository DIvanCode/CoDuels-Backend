using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GetUserSubmissionsHandlerTests
{
    private static (Context ctx, System.Data.Common.DbConnection conn) NewCtx()
        => DbContextFactory.CreateSqliteContext();

    private static Duel MakeDuel(int id, User u1, User u2)
        => new Duel
        {
            Id = id,
            TaskId = 1,
            User1 = u1,
            User2 = u2,
            Status = DuelStatus.InProgress,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            DeadlineTime = DateTime.UtcNow.AddMinutes(30)
        };

    [Fact]
    public async Task Returns_NotFound_when_duel_not_belongs_to_user()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user1 = new User { Id = 10 };
        var user2 = new User { Id = 11 };
        var otherUser = new User { Id = 99 };
        var duel = MakeDuel(20, user1, user2);

        ctx.AddRange(user1, user2, otherUser, duel);
        await ctx.SaveChangesAsync();

        var handler = new GetUserSubmissionsHandler(ctx);

        var result = await handler.Handle(new GetUserSubmissionsQuery
        {
            UserId = otherUser.Id,  // не участник дуэли
            DuelId = duel.Id
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_empty_list_when_no_submissions()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user1 = new User { Id = 10 };
        var user2 = new User { Id = 11 };
        var duel = MakeDuel(20, user1, user2);

        ctx.AddRange(user1, user2, duel);
        await ctx.SaveChangesAsync();

        var handler = new GetUserSubmissionsHandler(ctx);

        var result = await handler.Handle(new GetUserSubmissionsQuery
        {
            UserId = user1.Id,
            DuelId = duel.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_only_users_submissions_for_duel_sorted_by_time_and_verdict_only_when_done()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var u1 = new User { Id = 10 };
        var u2 = new User { Id = 11 };
        var duel = MakeDuel(20, u1, u2);
        var otherDuel = MakeDuel(21, u1, u2);

        var t = DateTime.UtcNow;

        // сабмишены пользователя u1 в нужной дуэли (разные статусы и времена)
        var s1 = new Submission
        {
            Id = 1, Duel = duel, User = u1,
            Status = SubmissionStatus.InProgress,
            SubmitTime = t.AddMinutes(-5),
            Language = "py", Code = "x"
        };
        var s2 = new Submission
        {
            Id = 2, Duel = duel, User = u1,
            Status = SubmissionStatus.Done, Verdict = "Accepted",
            SubmitTime = t.AddMinutes(-3),
            Language = "py", Code = "y"
        };
        var s3 = new Submission
        {
            Id = 3, Duel = duel, User = u1,
            Status = SubmissionStatus.Done, Verdict = "Wrong",
            SubmitTime = t.AddMinutes(-1),
            Language = "py", Code = "z"
        };

        // шум: другой пользователь в той же дуэли
        var sOtherUser = new Submission
        {
            Id = 4, Duel = duel, User = u2,
            Status = SubmissionStatus.Done, Verdict = "Accepted",
            SubmitTime = t.AddMinutes(-2)
        };

        // шум: тот же пользователь, но другая дуэль
        var sOtherDuel = new Submission
        {
            Id = 5, Duel = otherDuel, User = u1,
            Status = SubmissionStatus.Done, Verdict = "Accepted",
            SubmitTime = t.AddMinutes(-10)
        };

        ctx.AddRange(u1, u2, duel, otherDuel, s1, s2, s3, sOtherUser, sOtherDuel);
        await ctx.SaveChangesAsync();

        var handler = new GetUserSubmissionsHandler(ctx);

        var result = await handler.Handle(new GetUserSubmissionsQuery
        {
            UserId = u1.Id,
            DuelId = duel.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var list = result.Value;

        // только 3 сабмишена u1 для duel.Id == 20
        list.Should().HaveCount(3);
        list.Select(x => x.SubmissionId).Should().BeEquivalentTo(new[] { 1, 2, 3 });

        // по времени по возрастанию
        list.Select(x => x.SubmissionId).Should().ContainInOrder(1, 2, 3);

        // Verdict только когда Status == Done
        list.Single(x => x.SubmissionId == 1).Verdict.Should().BeNull();
        list.Single(x => x.SubmissionId == 2).Verdict.Should().Be("Accepted");
        list.Single(x => x.SubmissionId == 3).Verdict.Should().Be("Wrong");
    }
}
