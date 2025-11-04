using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class SendSubmissionHandlerTests
{
    private static (Context ctx, System.Data.Common.DbConnection conn) NewCtx()
        => DbContextFactory.CreateSqliteContext();

    [Fact]
    public async Task Returns_NotFound_when_duel_does_not_exist()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var taski = new Mock<ITaskiClient>(MockBehavior.Strict);
        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var cmd = new SendSubmissionCommand
        {
            DuelId = 999, UserId = 1, Code = "print(42)", Language = "python"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        taski.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_NotFound_when_user_does_not_exist()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        // есть дуэль, но нет пользователя
        var duel = new Duel
        {
            Id = 10, TaskId = 777, Status = DuelStatus.InProgress,
            StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddMinutes(30)
        };
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>(MockBehavior.Strict);
        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var cmd = new SendSubmissionCommand
        {
            DuelId = duel.Id, UserId = 123, Code = "x", Language = "py"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        taski.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Creates_submission_with_Queued_status_saves_and_calls_TaskiClient()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user = new User { Id = 1 };
        var duel = new Duel
        {
            Id = 10, TaskId = 321, Status = DuelStatus.InProgress,
            StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddMinutes(45)
        };
        ctx.AddRange(user, duel);
        await ctx.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>();
        taski.Setup(t => t.TestSolutionAsync(
                duel.TaskId,
                It.IsAny<string>(),
                "print(1)",
                "python",
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(Result.Ok())
             .Verifiable();

        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var before = DateTime.UtcNow.AddSeconds(-5);

        var cmd = new SendSubmissionCommand
        {
            DuelId = duel.Id,
            UserId = user.Id,
            Code = "print(1)",
            Language = "python"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        // результат успешный и корректный DTO
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.SubmissionId.Should().BeGreaterThan(0);
        dto.Solution.Should().Be("print(1)");
        dto.Language.Should().Be("python");
        dto.Status.Should().Be(SubmissionStatus.Queued);
        dto.SubmitTime.Should().BeOnOrAfter(before);

        // проверим, что запись реально в БД правильная
        var saved = await ctx.Submissions.SingleAsync(s => s.Id == dto.SubmissionId);
        saved.DuelId.Should().Be(duel.Id);
        saved.UserId.Should().Be(user.Id);
        saved.Status.Should().Be(SubmissionStatus.Queued);
        saved.Code.Should().Be("print(1)");
        saved.Language.Should().Be("python");

        // taskiClient вызван с ожидаемыми аргументами
        taski.Verify(t => t.TestSolutionAsync(
            duel.TaskId,
            dto.SubmissionId.ToString(),
            "print(1)",
            "python",
            It.IsAny<CancellationToken>()),
            Times.Once);
        taski.VerifyAll();
    }

    [Fact]
    public async Task Propagates_error_when_TaskiClient_fails()
    {
        var (ctx, conn) = NewCtx();
        await using var _ = conn;

        var user = new User { Id = 1 };
        var duel = new Duel
        {
            Id = 10, TaskId = 999, Status = DuelStatus.InProgress,
            StartTime = DateTime.UtcNow, DeadlineTime = DateTime.UtcNow.AddMinutes(45)
        };
        ctx.AddRange(user, duel);
        await ctx.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>();
        taski.Setup(t => t.TestSolutionAsync(
                duel.TaskId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(Result.Fail("judge unavailable"));

        var handler = new SendSubmissionHandler(ctx, taski.Object);

        var cmd = new SendSubmissionCommand
        {
            DuelId = duel.Id,
            UserId = user.Id,
            Code = "code",
            Language = "py"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("judge", StringComparison.OrdinalIgnoreCase));

        // сабмишен всё равно создан и сохранён 
        (await ctx.Submissions.CountAsync()).Should().Be(1);
    }
}
