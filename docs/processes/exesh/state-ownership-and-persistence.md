# State ownership and persistence

## Purpose

Provide the canonical inventory of Exesh control, business, file, and telemetry
state and show which parts can and cannot be recovered after restart.

## Participants

Coordinator domain/schedulers/worker pool/dispatcher, worker queue/providers,
PostgreSQL, coordinator and worker filestorage, runtimes, Kafka, external
consumers, and deployment filesystem/container lifecycle.

## Trigger

Use this inventory when reviewing any submission, scheduling, heartbeat,
artifact, result, message, retry, restart, or multi-coordinator change.

## Preconditions

“Survives restart” assumes the backing PostgreSQL/Kafka system or filesystem
volume itself survives. Current production Ansible does not attach explicit
durable volumes to coordinator/worker filestorage containers.

## Current behavior

The durable execution aggregate is only a definition and coarse status row.
Every actionable job/stage/worker state is reconstructed in memory. PostgreSQL
persists public messages, optional outbox intents, category statistics, and
best-effort telemetry, but none of those tables is read to restore graph or job
progress. Local filestorage can retain bytes until TTL/deletion, yet Exesh also
needs process-local source and artifact maps that are not reconstructed by
scanning those bytes.

## State transitions

Durable execution transitions are `new -> scheduled -> finished`. History and
histograms append/increment. Outbox transitions pending-to-deleted. All finer
transitions—ready, promised, started, queued, running, completed pending,
artifact advertised—are conceptual states represented by volatile data
structures and disappear on owner restart.

## State ownership

| State | Owner | Where stored | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Execution definition (sources/stages/weight/timestamps) | execution storage | PostgreSQL `Executions` | Yes | PostgreSQL row |
| Execution status | execution domain/storage | PostgreSQL `Executions.status` and live copy | Yes | PostgreSQL, though live copy can diverge until save |
| Execution tries | execution domain/storage | PostgreSQL and live copy | Yes, but semantics mix scheduling/completion | PostgreSQL |
| Active execution object | execution scheduler | `executions` map | No | Current coordinator heap |
| Execution graph/dependency/cancellation counters | domain graph | Coordinator heap | No | Graph maps under mutex |
| Ready jobs | graph/scheduler wrapper | `toPick` and FIFO | No | Current queues |
| Promised jobs/start predictions | job scheduler | Slice | No | `promisedJobs` |
| Started jobs/callbacks/worker assignment | job scheduler | Map by job ID | No | `startedJobs` |
| Coordinator capacity usage | execution scheduler | Atomic `nowWeight` | No | Current process counter |
| Worker registry and last heartbeat | worker pool | Map | No | Current coordinator heap |
| Worker predicted running jobs/resources | worker pool | Per-worker maps/counter | No | Coordinator predictions |
| Artifact locations and expiry | worker pool | Per-worker map | No | Coordinator advertisement map |
| Worker local job queue | worker | Heap queue | No | Worker process |
| Worker free slots/available memory | worker | Heap counters | No | Worker counters; coordinator has a separate prediction |
| Completed results before heartbeat | worker | `doneJobs` slice | No | Worker process |
| Runtime/chain resource paths | executor/runtime | Heap + temp/isolate directory | No | Runtime object/files |
| Source definitions | execution storage | PostgreSQL JSONB | Yes | PostgreSQL |
| Materialized source/job/output maps | execution factory | Coordinator heap | No | Active execution object |
| Coordinator cached sources | coordinator filestorage | Local filesystem/meta | Only if filesystem survives TTL/restart | Local filestorage |
| Worker cached source bytes | worker filestorage | Local filesystem/meta | Only if filesystem survives TTL/restart | Local filestorage |
| Worker cached source-ID locations | source provider | Heap map | No | Provider map; files alone are insufficient |
| Artifact bytes/trash time | worker output provider/filestorage | Local filesystem/meta | Only if filesystem survives TTL/restart | Local filestorage |
| Message history | message storage | PostgreSQL `Messages` | Yes | PostgreSQL |
| Outbox records | outbox storage | PostgreSQL `Outbox` | Yes | PostgreSQL |
| Kafka records | Kafka | Broker log | Broker retention dependent | Kafka |
| Consumer message cursor | Duely/Taski | Consumer-owned persistence | Consumer dependent | Consumer DB/state |
| Category estimates' samples | histogram storage | PostgreSQL histogram tables | Yes | PostgreSQL |
| Scheduler events | event recorder | Async channel then PostgreSQL event tables | Only after successful async insert | Best-effort telemetry |
| Event channel/backpressure | event recorder | Coordinator heap | No | In-memory channel |
| Dispatcher backoff | dispatcher | Loop local variables | No | Current process |

## Persistence and transaction boundaries

| Unit of work | PostgreSQL operations committed together | Important non-transactional side effects |
| --- | --- | --- |
| Submission | Histogram read; execution insert | HTTP response after commit |
| Schedule claim | Row claim; start history; optional outbox; scheduled save | Source downloads, active map, queues, event, weight |
| Successful result | Row lock; histogram increments; job messages/outbox; scheduled refresh | Started removal, artifact ad, graph progress, events |
| Finish | Finish history/outbox; finished save | Event, force flag, map delete, weight decrement |
| Outbox dispatch | Row select; delete or intended failure update | Kafka broker write |
| REST history read | Ordered select | Consumer applies after response |

`GetExecutionForSchedule` uses `FOR UPDATE SKIP LOCKED`; result lookup and
message sequencing use `FOR UPDATE`; outbox uses `FOR UPDATE` without skip. No
transaction spans coordinator memory, worker memory, local files, HTTP, Kafka,
and PostgreSQL.

## Idempotency and duplicate handling

Durable rows alone do not provide job-level idempotency. Deterministic IDs and
filestorage existing-file handling provide limited reuse. Message IDs order
history but do not semantically deduplicate attempts. Kafka key is outbox ID.
Worker/result dedupe relies on a volatile started map. Restart removes that
protection and recovery replays the whole definition.

## Concurrency and races

Mutexes protect individual in-process maps/queues, PostgreSQL locks protect
individual row operations, and filestorage locks protect local buckets/files.
No lock covers an entire end-to-end attempt or crosses coordinator instances.
Consequently stale overlap, double finish, worker-removal/result races, map/DB
divergence, and global capacity overcommit remain possible.

## Failure handling

Only PostgreSQL-backed definitions/status/history/outbox/histograms and already
inserted events are naturally available after process restart. Stale replay is
the only execution recovery mechanism. It cannot identify completed jobs or
locate artifacts, so it repeats all work. Local files may remain orphaned until
TTL. See [Failure and recovery](failure-and-recovery.md) for the full matrix.

## Emitted messages/events

| State change | Public history/outbox | Scheduler telemetry |
| --- | --- | --- |
| Execution accepted `new` | None | None |
| Scheduling begins | `start` | `started` |
| Candidate/placement/promise | None | `picked_candidate`, `promised`, `started`, worker events |
| Job result | Compile/run/check if non-error | `finished` job event |
| Worker expires | None | `removed` worker event |
| Execution terminates | `finish` | `finished` execution event |

## Observability

Public history is durable application output; scheduler event tables are
best-effort diagnostics with seven-day retention; logs are process output; the
custom Prometheus surface is only coordinator `now_weight`. None is a control-
plane source for recovery.

## Implementation references

- `Exesh/internal/domain/execution`
- `Exesh/internal/scheduler`
- `Exesh/internal/worker/worker.go`
- `Exesh/internal/storage/postgres`
- `Exesh/internal/provider` and `internal/dispatcher`
- `Exesh/cmd/{coordinator,worker}/main.go`
- `Exesh/config` and `Exesh/ansible/deploy/playbook.yml`

## Current guarantees

Committed PostgreSQL records and broker/local-file data survive only according
to their backing system. In-memory state does not. No current code rebuilds
graph progress, promises, started assignments, worker-local result queues,
source-provider maps, or artifact location maps from persistent data.

## Open questions

Which rows form the authoritative job/attempt ledger? Which file state must be
durable? May telemetry ever drive recovery? What consistency should callers see
between finish history and status? See [Open questions](open-questions.md).

## Proposed requirements

- Decide and document a durable state machine for attempts/jobs/results.
- Add fencing/ownership and recoverable artifact metadata.
- Define transaction/outbox boundaries for every external side effect.
- Keep telemetry explicitly non-authoritative.
- Add restart tests proving every “survives” and “does not survive” claim.

## Test coverage

- **Existing tests / covered scenarios:** no Exesh state/persistence tests;
  filestorage tests cover only its filesystem subsystem.
- **Missing scenarios:** schemas, transaction composition, locks, restart,
  volatile-state loss, and heap/database divergence.
- **Required integration tests:** persist every table/file state, restart each
  owner independently, and assert the canonical inventory.
- **Required failure-injection tests:** crash at every transaction/filesystem/
  HTTP boundary and verify which side effects remain recoverable.
