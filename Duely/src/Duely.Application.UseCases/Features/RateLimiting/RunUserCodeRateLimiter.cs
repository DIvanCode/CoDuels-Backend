using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.RateLimiting;


public interface IRunUserCodeLimiter
{
    Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken);
}


public class RunUserCodeLimiter(Context context) : IRunUserCodeLimiter
{
    private const int RunLimit = 10;

    public async Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
    {
        var count = await context.UserCodeRuns
            .Where(r => r.User.Id == userId && r.CreatedAt >= DateTime.UtcNow.AddMinutes(-1))
            .CountAsync(cancellationToken);

        return count >= RunLimit;
    }
}