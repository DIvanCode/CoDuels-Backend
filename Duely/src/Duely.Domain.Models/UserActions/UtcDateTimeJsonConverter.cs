using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Duely.Domain.Models.UserActions;

public sealed partial class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex OffsetTimestampPattern();

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null || !OffsetTimestampPattern().IsMatch(value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            throw new JsonException("Timestamp must be an ISO 8601 value with an explicit offset.");
        }

        return timestamp.UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Kind == DateTimeKind.Utc
            ? value
            : throw new JsonException("Timestamp must be UTC."));
    }
}
