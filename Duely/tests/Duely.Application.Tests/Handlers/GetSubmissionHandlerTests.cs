using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.UseCases.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GetSubmissionHandlerTests
{
    private static (Context ctx, System.Data.Common.DbConnection conn) NewCtx()
        => DbContextFactory.CreateSqliteContext();

    [Fact]
    public async Task Returns_Dto_when_submission_found_and_ids_match()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user = new User { Id = 10 };
        var duel = new Duel { Id = 20, Status = DuelStatus.InProgress, StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddHours(1) };

        var submission = new Submission
        {
            Id = 30,
            User = user,
            Duel = duel,
            Code = "print(42)",
            Language = "python",
            Status = SubmissionStatus.Done,
            SubmitTime = DateTime.UtcNow.AddMinutes(-5),
            Message = "ok",
            Verdict = "Accepted"
        };

        ctx.AddRange(user, duel, submission);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);

        var result = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 30,
            UserId = 10,
            DuelId = 20
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Should().BeOfType<SubmissionDto>();
        dto.SubmissionId.Should().Be(30);
        dto.Solution.Should().Be("print(42)");
        dto.Language.Should().Be("python");
        dto.Status.Should().Be(SubmissionStatus.Done);
        dto.Message.Should().Be("ok");
        dto.Verdict.Should().Be("Accepted");
        dto.SubmitTime.Should().BeCloseTo(submission.SubmitTime, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Returns_NotFound_when_submission_absent()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var handler = new GetSubmissionHandler(ctx);

        var result = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 999,
            UserId = 1,
            DuelId = 2
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_NotFound_when_user_mismatch()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user = new User { Id = 10 };
        var otherUser = new User { Id = 11 };
        var duel = new Duel { Id = 20, Status = DuelStatus.InProgress, StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddHours(1) };

        var submission = new Submission
        {
            Id = 30,
            User = user, // фактический автор — 10
            Duel = duel,
            Code = "x",
            Language = "py",
            Status = SubmissionStatus.Done,
            SubmitTime = DateTime.UtcNow
        };

        ctx.AddRange(user, otherUser, duel, submission);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);

        var result = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 30,
            UserId = 11,   // запрашиваем "не того" пользователя
            DuelId = 20
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_NotFound_when_duel_mismatch()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user = new User { Id = 10 };
        var duel = new Duel { Id = 20, Status = DuelStatus.InProgress, StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddHours(1) };
        var otherDuel = new Duel { Id = 21, Status = DuelStatus.InProgress, StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddHours(1) };

        var submission = new Submission
        {
            Id = 30,
            User = user,
            Duel = duel, // фактическая дуэль — 20
            Code = "x",
            Language = "py",
            Status = SubmissionStatus.Done,
            SubmitTime = DateTime.UtcNow
        };

        ctx.AddRange(user, duel, otherDuel, submission);
        await ctx.SaveChangesAsync();

        var handler = new GetSubmissionHandler(ctx);

        var result = await handler.Handle(new GetSubmissionQuery
        {
            SubmissionId = 30,
            UserId = 10,
            DuelId = 21 // запрошена другая дуэль
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
