using System;
using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.RateLimiting;
using Duely.Application.UseCases.Features.Submissions;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

public class SubmissionRateLimiterTests : ContextBasedTest
{
    [Fact]
    public async Task ReturnsFalse_when_limit_not_exceeded()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);

        // Добавляем 2 посылки (меньше лимита 5)
        var sub1 = EntityFactory.MakeSubmission(1, duel, u1, time: DateTime.UtcNow.AddSeconds(-30));
        var sub2 = EntityFactory.MakeSubmission(2, duel, u1, time: DateTime.UtcNow.AddSeconds(-20));
        ctx.Submissions.AddRange(sub1, sub2);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { SubmissionsPerMinute = 5 });
        var limiter = new SubmissionRateLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsTrue_when_limit_exceeded()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);

        // Добавляем 5 посылок (равно лимиту)
        for (int i = 1; i <= 5; i++)
        {
            var sub = EntityFactory.MakeSubmission(i, duel, u1, time: DateTime.UtcNow.AddSeconds(-i * 10));
            ctx.Submissions.Add(sub);
        }
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { SubmissionsPerMinute = 5 });
        var limiter = new SubmissionRateLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Ignores_submissions_older_than_one_minute()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);

        // Старая посылка (больше минуты назад)
        var oldSub = EntityFactory.MakeSubmission(1, duel, u1, time: DateTime.UtcNow.AddMinutes(-2));
        // 4 новые посылки
        for (int i = 2; i <= 5; i++)
        {
            var sub = EntityFactory.MakeSubmission(i, duel, u1, time: DateTime.UtcNow.AddSeconds(-i * 10));
            ctx.Submissions.Add(sub);
        }
        ctx.Submissions.Add(oldSub);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { SubmissionsPerMinute = 5 });
        var limiter = new SubmissionRateLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse(); // Старая посылка не учитывается
    }

    [Fact]
    public async Task Counts_only_submissions_for_specific_user()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        var duel = EntityFactory.MakeDuel(10, u1, u2, "TASK");
        ctx.Duels.Add(duel);

        // u2 отправил 5 посылок
        for (int i = 1; i <= 5; i++)
        {
            var sub = EntityFactory.MakeSubmission(i, duel, u2, time: DateTime.UtcNow.AddSeconds(-i * 10));
            ctx.Submissions.Add(sub);
        }
        // u1 отправил только 1 посылку
        var u1Sub = EntityFactory.MakeSubmission(6, duel, u1, time: DateTime.UtcNow.AddSeconds(-10));
        ctx.Submissions.Add(u1Sub);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { SubmissionsPerMinute = 5 });
        var limiter = new SubmissionRateLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse(); // u1 не превысил лимит
    }

    [Fact]
    public async Task ReturnsFalse_when_no_submissions()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { SubmissionsPerMinute = 5 });
        var limiter = new SubmissionRateLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse();
    }
}

