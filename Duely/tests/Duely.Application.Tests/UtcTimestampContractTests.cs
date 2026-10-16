using System.Text.Json;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.UserActions;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests;

public sealed class UtcTimestampContractTests
{
    [Fact]
    public void User_dto_serializes_utc_timestamps_with_z_suffix()
    {
        var dto = new UserDto
        {
            Id = 1,
            Nickname = "test",
            Rating = 1500,
            CreatedAt = new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc),
            LastSeenAt = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc)
        };

        JsonSerializer.Serialize(dto).Should().Contain("\"created_at\":\"2026-03-29T00:30:00Z\"");
        JsonSerializer.Serialize(dto).Should().Contain("\"last_seen_at\":\"2026-03-29T01:30:00Z\"");
    }

    [Fact]
    public void User_dto_serializes_null_last_seen()
    {
        var dto = new UserDto
        {
            Id = 1,
            Nickname = "test",
            Rating = 1500,
            CreatedAt = new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc)
        };

        JsonSerializer.Serialize(dto).Should().Contain("\"last_seen_at\":null");
    }

    [Theory]
    [InlineData("2026-03-29T00:30:00Z")]
    [InlineData("2026-03-29T03:30:00+03:00")]
    [InlineData("2026-03-28T19:30:00-05:00")]
    public void Browser_action_timestamp_normalizes_explicit_offset(string value)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());

        var timestamp = JsonSerializer.Deserialize<DateTime>($"\"{value}\"", options);
        timestamp.Should().Be(new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc));
        timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Polymorphic_browser_action_uses_utc_timestamp_converter()
    {
        const string json = """
            {"type":"ChooseLanguage","event_id":"94ae475c-f025-4a60-a511-a398c48c76bb","sequence_id":1,"timestamp":"2026-03-29T03:30:00+03:00","duel_id":2,"task_key":"A","user_id":3,"language":"Python"}
            """;

        var action = JsonSerializer.Deserialize<UserAction>(json);
        action.Should().BeOfType<ChooseLanguageUserAction>();
        action!.Timestamp.Should().Be(new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc));
        JsonSerializer.Serialize(action).Should().Contain("\"timestamp\":\"2026-03-29T00:30:00Z\"");
    }

    [Theory]
    [InlineData("2026-03-29T00:30:00")]
    [InlineData("2026-02-30T00:30:00Z")]
    public void Browser_action_timestamp_rejects_ambiguous_or_invalid_values(string value)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());

        Action parse = () => JsonSerializer.Deserialize<DateTime>($"\"{value}\"", options);
        parse.Should().Throw<JsonException>();
    }

    [Fact]
    public void Persisted_instants_require_utc_and_use_timestamptz()
    {
        var instant = new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc);
        UtcDateTimeConverter.RequireUtc(instant).Should().Be(instant);
        Action persistLocal = () => UtcDateTimeConverter.RequireUtc(instant.ToLocalTime());
        Action persistUnspecified = () => UtcDateTimeConverter.RequireUtc(DateTime.SpecifyKind(instant, DateTimeKind.Unspecified));
        persistLocal.Should().Throw<ArgumentException>();
        persistUnspecified.Should().Throw<ArgumentException>();
        UtcDateTimeConverter.NormalizeRead(instant).Should().Be(instant);
        UtcDateTimeConverter.NormalizeRead(instant.ToLocalTime()).Should().Be(instant);
        UtcDateTimeConverter.NormalizeRead(
            DateTime.SpecifyKind(instant, DateTimeKind.Unspecified)).Should().Be(instant);

        using var context = new Context(new DbContextOptionsBuilder<Context>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);
        var timestampProperties = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            .Distinct()
            .ToArray();

        timestampProperties.Should().NotBeEmpty();
        timestampProperties.Should().OnlyContain(property => property.GetColumnType() == "timestamp with time zone");
        timestampProperties.Should().OnlyContain(property => property.GetTypeMapping().Converter is UtcDateTimeConverter);
    }
}
