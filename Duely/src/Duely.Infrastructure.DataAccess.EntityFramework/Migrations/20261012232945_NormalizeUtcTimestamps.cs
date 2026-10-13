using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations;

public partial class NormalizeUtcTimestamps : Migration
{
    private static readonly (string Table, string Column)[] InstantColumns =
    [
        ("Users", "CreatedAt"),
        ("UserActions", "Timestamp"),
        ("Tournaments", "CreatedAt"),
        ("Submissions", "SubmitTime"),
        ("Submissions", "CompletedAt"),
        ("PendingDuels", "CreatedAt"),
        ("Outbox", "RetryUntil"),
        ("Outbox", "RetryAt"),
        ("Duels", "StartTime"),
        ("Duels", "EndTime"),
        ("Duels", "DeadlineTime"),
        ("CodeRuns", "CreatedAt")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing values were written as UTC wall-clock values. The explicit
        // zone keeps conversion independent of the database session timezone.
        foreach (var (table, column) in InstantColumns)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}" ALTER COLUMN "{column}"
                TYPE timestamp with time zone
                USING "{column}" AT TIME ZONE 'UTC';
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, column) in InstantColumns)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}" ALTER COLUMN "{column}"
                TYPE timestamp without time zone
                USING "{column}" AT TIME ZONE 'UTC';
                """);
        }
    }
}
