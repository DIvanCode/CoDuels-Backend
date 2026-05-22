using Microsoft.EntityFrameworkCore;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public static class QueryExtensions
{
    public const string ForUpdateTag = "EF_FOR_UPDATE";

    public static IQueryable<T> ForUpdate<T>(this IQueryable<T> query)
    {
        return query.TagWith(ForUpdateTag);
    }
}
