using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Users;

internal static class UserLastSeenUpdater
{
    public static async Task RecordAsync(
        Context context,
        int userId,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        // A conditional database update prevents a delayed login or socket event
        // from overwriting a newer timestamp written in another request scope.
        await context.Users
            .Where(user => user.Id == userId &&
                (user.LastSeenAt == null || user.LastSeenAt < occurredAt))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.LastSeenAt, occurredAt),
                cancellationToken);
    }
}
