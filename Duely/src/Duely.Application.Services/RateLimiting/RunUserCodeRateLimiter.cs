using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Duely.Application.Services.RateLimiting;

public interface IRunUserCodeLimiter
{
    Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken);
}

public class RunUserCodeLimiter(Context context, IOptions<RateLimitingOptions> options) : IRunUserCodeLimiter
{
    public async Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
    {
        var count = await context.CodeRuns
            .Where(r => r.User.Id == userId && r.CreatedAt >= DateTime.UtcNow.AddMinutes(-1))
            .CountAsync(cancellationToken);

        return count >= options.Value.RunsPerMinute;
    }
}
