using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => RequireUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    public static DateTime RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Persisted instants must use UTC DateTime values.", nameof(value));
        }

        return value;
    }
}
