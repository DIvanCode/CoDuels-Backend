using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.RateLimiting;


public interface ISubmissionRateLimiter
{
    Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken);
}


public class SubmissionRateLimiter(Context context) : ISubmissionRateLimiter
{
    private const int SubmitLimit = 5;

    public async Task<bool> IsLimitExceededAsync(int userId, CancellationToken cancellationToken)
    {
        var count = await context.Submissions
            .Where(s => s.User.Id == userId && s.SubmitTime >= DateTime.UtcNow.AddMinutes(-1))
            .CountAsync(cancellationToken);

        return count >= SubmitLimit;
    }
    
}