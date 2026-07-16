# State ownership and persistence

## Purpose

Identify the authoritative owner, durability, locks, and cross-service
transaction boundaries for every Taski process state.

## Participants

Taski API/use cases, filestorage, PostgreSQL, Exesh, Kafka, Duely, in-memory
pollers/dispatchers, and deployment configuration.

## Trigger

Any upload, read, submission, event, message, restart, reconciliation analysis,
or cross-service change.

## Preconditions

The services use their configured independent databases/storage. Production
selects Taski REST event consumer and Duely REST testing-message consumer.

## Current behavior

| State | Owner | Storage | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Task package and metadata | Taski/filestorage | bucket directory | Yes | committed bucket |
| Task ID | Taski import contract | bucket name/task references | Yes | bucket ID |
| Bucket read/write lock | filestorage | storage lock state | Implementation-dependent | filestorage |
| Submitted text/language/link IDs/timestamps | Taski | `Solutions` columns | Yes | Taski PostgreSQL |
| Graph, job outcomes, status, verdict | Taski strategy | Solution JSONB | Yes | Taski strategy copy |
| Execution/job/artifact lifecycle | Exesh | Exesh DB and workers | Partly; see Exesh docs | Exesh |
| Exesh event history and Message ID | Exesh | Exesh PostgreSQL | Yes | Exesh history |
| Taski Exesh-event cursor | Taski | `handled_events_count` | Yes | Taski Solution |
| Taski public history and Message ID | Taski | `Messages` | Yes | Taski PostgreSQL |
| Kafka publish intent | Taski | `Outbox` | Yes | Taski PostgreSQL |
| Kafka record/group offset | Kafka | broker | Yes per retention | Kafka |
| Duely testing-message cursor/status | Duely | Submission row | Yes | Duely PostgreSQL |
| Current page/ticker/HTTP request | poller/dispatcher | process memory | No | recomputed/retried |
| Mode/interval/endpoints | deployment | config/environment | On redeploy | effective runtime config |

No store contains one atomic end-to-end record. Taski's graph copy determines
verdict interpretation; Exesh owns whether jobs actually ran; Duely owns the
browser-facing Submission after consuming Taski history.

**Current guarantees.** Committed buckets, Taski rows/messages, Exesh history,
and Duely rows survive their process restarts. Local UoWs atomically cover only
one service. Production recovery relies on two persistent REST histories and
two separate persisted counts.

## State transitions

`Polygon files -> committed task bucket`; `request -> Exesh execution -> Taski
Solution`; `Exesh history -> Taski strategy/cursor/message`; `Taski history ->
Duely Submission/cursor`. Each arrow crosses at least one non-atomic boundary.

## State ownership

The table above is normative only for current ownership. Exesh `ExecutionID`,
Taski `ExternalSolutionID`, Exesh history ID, Taski history ID, Taski
`HandledEventsCount`, and Duely `HandledStatusCount` are six distinct values.

## Persistence and transaction boundaries

| Boundary | Atomic together | Explicitly not atomic |
| --- | --- | --- |
| Bucket reserve/commit | Files in one committed bucket publication | PostgreSQL, uploader subprocess effects |
| Submission UoW | Taski Solution insert | Exesh acceptance and filestorage state |
| Event UoW | Solution/strategy/cursor + Taski Message + optional Outbox | Exesh history, Kafka offset |
| Outbox delivery UoW | Taski select/delete/failure update if committed | Kafka publish |
| Duely update | Duely Submission + its WebSocket outbox | Taski message/history cursor ownership |

`GetByExecutionID` and outbox/message operations use `FOR UPDATE`. Task reads
use bucket locks. Taski holds its submission DB transaction and bucket read lock
while calling Exesh.

## Idempotency and duplicate handling

No global idempotency exists. Filestorage prevents duplicate bucket name;
PostgreSQL does not make external/execution Solution IDs unique; REST event and
Duely cursors use counts; Kafka paths lack inbox IDs; public message numeric IDs
prevent only identical primary keys; outbox/Kafka can duplicate delivery.

## Ordering assumptions

Exesh and Taski histories are independently contiguous from one. Execution
events are assumed lifecycle ordered. Public messages are assumed start/status/
finish ordered. Pollers process pages/rows sequentially. Broker record keys do
not explicitly guarantee per-execution/per-solution partitioning.

## Concurrency and race conditions

Cross-service locks do not compose. Exesh can emit before Taski commit; Duely
can retry while Taski is ambiguous; multiple pollers can select the same row;
duplicate IDs undermine row ownership; bucket lifetime after submission is not
leased to Exesh; slow external calls hold local locks/transactions.

## Failure handling

Each owner restarts from its durable state, but there is no end-to-end
reconciler. Orphan Exesh executions, permanently in-progress Solutions, missing
early Kafka events, incompatible strategy JSON, lost worker artifacts, and
duplicate public delivery require manual diagnosis or remain invisible.

## Emitted messages

| Condition | Message type | Recipient/channel | Payload | Persistence | Retry |
| --- | --- | --- | --- | --- | --- |
| Exesh progress | raw event/history | Taski | execution/job/status | Exesh history/Kafka | consumer-mode dependent |
| Taski derived progress | testing history/Kafka | Duely | external ID/status/verdict | Taski history/outbox | delivery-mode dependent |
| Duely update | WebSocket outbox | users | submission/duel state | Duely outbox | Duely policy |

## Observability

Databases/history/logs provide fragmented evidence. Task metrics include task
count labels and average solution process time. There is no shared trace,
state-reconciliation view, cursor/lag gauge, orphan/duplicate detector, bucket
lock/lifetime metric, stuck-solution/outbox backlog SLI, or compatibility alarm.

## Implementation references

- `Taski/internal/storage/{filestorage,postgres}`
- `Taski/internal/usecase/testing/usecase/{test,update,messages}`
- `Taski/internal/consumer/*.go`
- `Taski/internal/{dispatcher,producer,metrics}`
- `Taski/cmd/taski/main.go`
- `Taski/ansible/deploy/playbook.yml`
- `Exesh/internal/storage/postgres`
- `Duely/src/Duely.Infrastructure.BackgroundJobs/TaskiSubmissionStatusRestPoller.cs`

## Test coverage

- **Existing unit/integration tests:** Taski has none.
- **Covered scenarios:** none of the end-to-end ownership/transaction claims are
  proven by Taski tests.
- **Missing scenarios:** all restart, locking, uniqueness, cross-service crash,
  cursor, duplicate, retention, compatibility, and multi-instance paths.
- **Required contract tests:** persisted schemas/IDs/cursors and the complete
  Taski↔Exesh and Taski→Duely chain.
- **Required failure-injection tests:** death at every table boundary, bucket
  lock/process restart, Exesh/Taski/Duely/Kafka/PostgreSQL outage, history gap,
  concurrent instances, and rolling version skew.

## Open questions

Reconciliation ownership, retention, leases, cardinality, cursor semantics,
multi-instance topology, and source-of-truth conflict resolution are undefined.

## Proposed requirements

Publish explicit ownership/cardinality/retention contracts; persist dispatch
intent and last-seen event IDs; add reconcilers and leases where needed;
version persisted/cross-service schemas; instrument every boundary; and verify
restart/concurrency/failure guarantees end to end.

