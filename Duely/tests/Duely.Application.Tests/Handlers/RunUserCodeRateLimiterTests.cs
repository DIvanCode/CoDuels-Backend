using Duely.Application.Services.RateLimiting;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Duely.Application.Tests.Handlers;

public class RunUserCodeRateLimiterTests : ContextBasedTest
{
    [Fact]
    public async Task ReturnsFalse_when_limit_not_exceeded()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);

        // Добавляем 5 запусков (меньше лимита 10)
        for (int i = 1; i <= 5; i++)
        {
            var run = new CodeRun
            {
                Id = i,
                User = u1,
                Code = "print(1)",
                Language = Language.Python,
                Input = "test",
                Status = UserCodeRunStatus.Done,
                CreatedAt = DateTime.UtcNow.AddSeconds(-i * 10)
            };
            ctx.CodeRuns.Add(run);
        }
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { RunsPerMinute = 10 });
        var limiter = new RunUserCodeLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsTrue_when_limit_exceeded()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);

        // Добавляем 10 запусков (равно лимиту)
        for (int i = 1; i <= 10; i++)
        {
            var run = new CodeRun
            {
                Id = i,
                User = u1,
                Code = "print(1)",
                Language = Language.Python,
                Input = "test",
                Status = UserCodeRunStatus.Done,
                CreatedAt = DateTime.UtcNow.AddSeconds(-i * 5)
            };
            ctx.CodeRuns.Add(run);
        }
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { RunsPerMinute = 10 });
        var limiter = new RunUserCodeLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Ignores_runs_older_than_one_minute()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);

        // Старый запуск (больше минуты назад)
        var oldRun = new CodeRun
        {
            Id = 1,
            User = u1,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test",
            Status = UserCodeRunStatus.Done,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        // 9 новых запусков
        for (int i = 2; i <= 10; i++)
        {
            var run = new CodeRun
            {
                Id = i,
                User = u1,
                Code = "print(1)",
                Language = Language.Python,
                Input = "test",
                Status = UserCodeRunStatus.Done,
                CreatedAt = DateTime.UtcNow.AddSeconds(-i * 5)
            };
            ctx.CodeRuns.Add(run);
        }
        ctx.CodeRuns.Add(oldRun);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { RunsPerMinute = 10 });
        var limiter = new RunUserCodeLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse(); // Старый запуск не учитывается
    }

    [Fact]
    public async Task Counts_only_runs_for_specific_user()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        // u2 создал 10 запусков
        for (int i = 1; i <= 10; i++)
        {
            var run = new CodeRun
            {
                Id = i,
                User = u2,
                Code = "print(1)",
                Language = Language.Python,
                Input = "test",
                Status = UserCodeRunStatus.Done,
                CreatedAt = DateTime.UtcNow.AddSeconds(-i * 5)
            };
            ctx.CodeRuns.Add(run);
        }
        // u1 создал только 1 запуск
        var u1Run = new CodeRun
        {
            Id = 11,
            User = u1,
            Code = "print(1)",
            Language = Language.Python,
            Input = "test",
            Status = UserCodeRunStatus.Done,
            CreatedAt = DateTime.UtcNow.AddSeconds(-10)
        };
        ctx.CodeRuns.Add(u1Run);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { RunsPerMinute = 10 });
        var limiter = new RunUserCodeLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse(); // u1 не превысил лимит
    }

    [Fact]
    public async Task ReturnsFalse_when_no_runs()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new RateLimitingOptions { RunsPerMinute = 10 });
        var limiter = new RunUserCodeLimiter(ctx, options);

        var result = await limiter.IsLimitExceededAsync(1, CancellationToken.None);

        result.Should().BeFalse();
    }
}
