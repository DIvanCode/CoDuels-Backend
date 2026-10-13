# UTC timestamp contract

## Current behavior after issue 289

Every Duely date-time field represents an absolute instant. Duels use
`StartTime`, `DeadlineTime`, and `EndTime`; submissions use `SubmitTime` and
`CompletedAt`; users, code runs, pending duels, and tournaments use `CreatedAt`;
user actions use `Timestamp`; outbox messages use `RetryAt` and `RetryUntil`.
Group duel and invitation DTOs expose the pending duel `CreatedAt`. Tournament
group-stage DTOs expose the duel times. Ranked search DTOs expose the pending
duel creation time as `search_started_at`. No current public field is a
date-only value or a local wall-clock schedule.

All twelve persisted columns use PostgreSQL `timestamp with time zone`.
Application code writes UTC `DateTime` values. EF's common converter rejects
`Local` and `Unspecified` values. Npgsql's legacy timestamp switch is disabled,
so native `timestamp with time zone` reads arrive as UTC. The converter also
converts local-kind reads to UTC and interprets kindless provider reads as UTC
wall-clock values; materialized values have UTC kind.
Background jobs and deadline/rate-limit comparisons use `DateTime.UtcNow`.
Public HTTP and WebSocket JSON serializes UTC `DateTime` values as ISO 8601
with `Z`. Browser action timestamps must include `Z` or an explicit offset;
their offset is normalized to UTC before persistence.

## Migration and rollback

Before this migration the columns were `timestamp without time zone`, but the
application paths that created their values used `DateTime.UtcNow`, and browser
actions used `Date.toISOString()`. The migration therefore interprets existing
wall-clock values with `AT TIME ZONE 'UTC'`, regardless of the PostgreSQL
session timezone. It runs once through EF migration history inside the normal
migration transaction. A retry after failure starts from the original schema;
a completed migration is not applied again.

The down migration converts each instant back to a UTC wall-clock timestamp
using `AT TIME ZONE 'UTC'`. Rolling back also requires the old application to
be restored. Before applying this migration to a database with data imported
from any non-UTC source, inspect that source and correct those rows separately;
their original timezone cannot be inferred from a `timestamp` column.

## Verification

Run the Duely test suite for converter and serialization coverage. Apply the
migration to a copy of an existing-schema database with sample rows near UTC
midnight and a daylight-saving boundary, check the resulting instants under
different PostgreSQL session timezones, then apply the down migration and
confirm the original wall-clock values return.
