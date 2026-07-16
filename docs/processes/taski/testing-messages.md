# Testing messages

## Purpose

Expose Taski testing progress to Duely through a stable `start`/`status`/`finish`
contract independent of raw Exesh job events.

## Participants

Taski update use case/strategy, message dispatcher, Taski history/outbox,
Kafka producer (optional), REST messages API, and Duely Kafka consumer or REST
poller.

## Trigger

An Exesh event causes a start, changed intermediate status, or finish outcome.

## Preconditions

The Solution has an `ExternalSolutionID`; message storage can lock/find its
Solution row; Duely identifies that value as its Submission ID.

## Current behavior

Messages are:

- `start`: `{solution_id, type:"start"}`;
- `status`: `{solution_id, type:"status", status}`;
- `finish`: `{solution_id, type:"finish", verdict, error?, message?}`.

The `solution_id` is Taski `ExternalSolutionID`, not Taski's row ID or Exesh
`ExecutionID`. Job events are not public. Start is emitted for every processed
start duplicate; status only when its text differs from
`LastTestingStatus`; finish is generated on the first processed finish and may
contain a verdict or Exesh error.

Production disables Taski Kafka dispatch. Duely production polls
`/solutions/{submissionId}/messages?start_id=HandledStatusCount+1&count=20`,
validates response `status=OK`, orders by Taski Event ID, and applies each
message. Duely increments its own `HandledStatusCount` for every applied
message, sets Running for start/status, terminal Technical error for error, or
Done/exact verdict for verdict, and emits its own WebSocket outbox messages.

**Current guarantees.** Taski message history and Solution mutation are atomic.
Within one external ID, Taski allocates increasing IDs under Solution row locks
if that external ID maps to exactly one row. Production REST history persists
across process restarts.

## State transitions

`No Taski public message -> start -> zero or more changed status -> finish` is
the intended sequence. Actual code can produce duplicate starts, omit start,
finish early, or omit finish forever. Duely maps this to
`pending -> Running -> Done` conceptually.

## State ownership

| State | Owner | Storage | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Taski public message/ID | Taski | `Messages` | Yes | Taski PostgreSQL |
| Optional publish intent | Taski | `Outbox` | Yes | Taski PostgreSQL |
| Kafka record/offset | Kafka/consumer | broker | Yes | Kafka |
| Submission status/cursor | Duely | Duely PostgreSQL | Yes | Duely |

## Persistence and transaction boundaries

Message history/outbox and originating Solution update share a Taski
transaction. Kafka send/delete is later and non-atomic. Duely fetch and each
Duely status update use separate service/DB transactions, so Taski and Duely
cannot update atomically.

## Idempotency and duplicate handling

Taski history has PK `(solution_id,message_id)`, but no semantic event key.
Duplicate starts receive new IDs. Duely REST uses a count as cursor and its
handler increments once per message; repeated delivery with the same cursor is
suppressed only because the next fetch starts after the count. Kafka mode has
no Taski message ID in payload and no consumer dedupe, so duplicates reapply
and create Duely outbox notifications.

## Ordering assumptions

Both histories/cursors assume contiguous IDs with no gaps. Duely's REST poller
fetches only one page per Submission per tick (up to count) and orders it. Kafka
ordering is not explicitly keyed by external solution ID because Taski uses
outbox ID as record key.

## Concurrency and race conditions

Message allocation locks Solutions matching external ID. Multiple matching
rows make the insert CTE produce duplicate identical PK values and fail.
Concurrent valid generation for a unique row serializes. Duely polling multiple
instances relies on its own DB behavior/count but has no explicit claim lease.

## Failure handling

Taski message insert failure rolls back the Solution event, causing source-mode
retry. REST history errors leave Duely cursor unchanged for later retry. Duely
breaks on update failure. Kafka producer success followed by outbox transaction
failure duplicates. Missing finish leaves Duely nonterminal; an error finish
becomes `Technical error`.

## Emitted messages

| Condition | Message type | Recipient/channel | Payload | Persistence | Retry |
| --- | --- | --- | --- | --- | --- |
| Execution starts | `start` | Taski history; Kafka if enabled | external ID/type | History + optional Outbox | Event/outbox mode |
| Progress text changes | `status` | same | external ID/type/status | same | same |
| Execution finishes/errors | `finish` | same | external ID/type/verdict/error/message | same | same |
| Duely applies message | `SubmissionStatusUpdatedMessage` | submitting user WebSocket outbox | Duely submission view | Duely outbox | Duely policy |
| Accepted finish | `DuelChangedMessage` | both duel users | duel ID | Duely outbox | Duely policy |

## Observability

Taski REST history is queryable and logs expose fetch/generation failures;
Duely stores its cursor/status. There is no end-to-end correlation/lag,
duplicate, gap, backlog, consumer-count, or message-age metric.

## Implementation references

- `Taski/internal/domain/testing/message/messages/*.go`
- `Taski/internal/usecase/testing/usecase/update/usecase.go`
- `Taski/internal/usecase/testing/usecase/messages/usecase.go`
- `Taski/internal/api/testing/messages/*`
- `Duely/src/Duely.Infrastructure.BackgroundJobs/TaskiSubmissionStatusRestPoller.cs`
- `Duely/src/Duely.Infrastructure.Gateway.Tasks/TaskiClient.cs`
- `Duely/src/Duely.Application.UseCases/Features/Submissions/Update.cs`

## Test coverage

- **Existing unit/integration tests:** no Taski tests; no demonstrated cross-
  service contract suite.
- **Covered scenarios:** none in Taski automation.
- **Missing scenarios:** exact payloads/order, duplicate start/status/finish,
  missing/early finish, history pagination, Duely restart/concurrency, duplicate
  external IDs, Kafka duplicate, and transactional rollback.
- **Required contract tests:** Taski JSON/history response to both Duely
  consumers and exact verdict/status/error mapping.
- **Required failure-injection tests:** Taski commit failure, Duely update
  failure/restart, repeated page/Kafka record, cursor gap, concurrent generation
  and polling, outbox publish/delete failure, and missing finish.

## Open questions

Required public order/dedupe, retention, pagination SLA, message vocabulary,
Kafka support, and Duely reconciliation behavior are unspecified.

## Proposed requirements

Version the public envelope; include a durable message/event identity in every
transport; formalize order/gap/duplicate semantics and retention; enforce unique
external linkage; observe end-to-end lag; and contract-test Taski with both
Duely modes.

