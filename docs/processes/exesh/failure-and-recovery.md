# Failure and recovery

## Purpose

Describe what the current system preserves, loses, retries, duplicates, or
leaves stuck when a coordinator, worker, dependency, network, or storage
operation fails.

## Participants

Coordinator API/schedulers/worker pool/dispatcher, workers and runtimes,
PostgreSQL, coordinator and worker filestorage, heartbeat network, Kafka,
external source endpoints and consumers, container supervisor, and scheduler
telemetry.

## Trigger

Process restart, missed heartbeat, HTTP loss, stale execution timeout,
PostgreSQL/Kafka/filestorage failure, source/artifact expiry, runtime error, or
shutdown signal.

## Preconditions

Recovery depends only on durable execution definitions/status/timestamps,
history/outbox/histograms, local filesystem data that happens to survive, and
consumer state. There is no durable job/attempt/worker/artifact ledger.

## Current behavior

Coordinator restart clears active graphs, queues, promises, started jobs,
workers, artifact locations, `nowWeight`, and dispatcher backoff. Workers
re-register, but their previously completed results are unknown to the new job
scheduler. Every durable `scheduled` row older than 30s becomes eligible for a
full replay from its original definition, including already completed jobs and
messages.

Worker death is detected after missed heartbeat and deletes only its pool entry.
Its started jobs are not requeued or failed, and its artifacts lose their only
advertised location. The execution can remain active forever unless stale
execution replay starts another whole attempt. If the coordinator remains
alive, the old active object/weight also remains.

Heartbeat request failure makes the worker retry its entire result batch.
Response loss after coordinator processing can both duplicate results (usually
ignored in one process) and lose newly assigned jobs (coordinator already marked
them started). Worker restart loses pending results and queue.

PostgreSQL failure rolls back current database work, but graph/counter/map/file
mutations outside the transaction can remain. A successful job whose histogram,
message, or execution save fails becomes an error finish; its result is no
longer recognizable for retry. Finish persistence failure leaves durable
`scheduled` and triggers later full replay.

Coordinator source download failure rolls back claim but can leave filesystem
side effects or an in-memory execution entry. Worker source save failure logs and
still queues work. Artifact expiry/death yields no replica and fails dependency
resolution. Output save failure can produce a result claiming a missing artifact.

Kafka outage retains the outbox row, but failure metadata rolls back. Kafka
success followed by delete/commit failure duplicates publication. The oldest
row can block later rows. With Kafka disabled, REST history is unaffected.

Graceful shutdown stops HTTP servers before root context cancellation and does
not wait for scheduler/worker/outbox loops, drain jobs, flush results, close the
Kafka writer, or close PostgreSQL explicitly.

## State transitions

Coordinator restart: `active in memory -> lost -> durable scheduled -> stale ->
new full attempt`. Worker death: `registered -> removed`, while
`started job -> still started` in another map. Kafka: `pending -> attempted ->
pending again` on failure/commit uncertainty. None represent a durable job retry.

## State ownership

| State | Failure owner | Stored in | Survives failure | Recovery source |
| --- | --- | --- | --- | --- |
| Definition/status/timestamps | PostgreSQL | `Executions` | DB restart yes | Whole-definition replay |
| Graph/job progress | coordinator | Heap | No | Cannot resume |
| Promise/started/worker registry | coordinator | Heap | No | Cannot reconstruct |
| Worker queue/pending results | worker | Heap | No | Cannot reconstruct |
| Artifact locations | coordinator | Heap | No | Cannot rediscover automatically |
| Source/artifact files | local filestorage | Container filesystem | Only if filesystem survives/TTL | Local scan not integrated with scheduler |
| History/outbox/histograms | PostgreSQL | Tables | Yes | Durable rows |
| Scheduler events | async recorder | Tables after write | Partial | Diagnostic only |

## Persistence and transaction boundaries

`FOR UPDATE SKIP LOCKED` serializes one claim transaction but does not fence
attempt lifetime. Result transactions combine histograms/history/status but not
graph/started/artifact state. Finish combines history/status but removes local
state first. Outbox write/delete wraps broker I/O in a DB transaction without a
distributed commit. Filesystem operations never share atomicity with PostgreSQL.

## Idempotency and duplicate handling

Deterministic IDs and existing-file handling reduce some file duplication.
Started-result removal ignores simple redelivery. No durable idempotency covers
submission, execution attempts, job commands, samples, messages, finish, or
Kafka. Recovery is replay and can duplicate all of them; consumer-side cursor or
Kafka-key dedupe is external.

## Concurrency and races

Stale selection can overlap a live attempt on the same or another coordinator.
Old callbacks and new attempts share deterministic job IDs without fencing.
Worker removal races result/artifact handling. Concurrent terminal callbacks can
double finish/weight decrement. Multiple coordinators serialize short DB claims
and outbox rows but have independent capacity/workers/graphs and can exceed
global limits.

## Failure handling

The matrix records current outcomes, not desired guarantees.

| Failure | Detected by | Current action | Durable outcome | Main risk |
| --- | --- | --- | --- | --- |
| Invalid definition after accept | scheduler factory | Log and rollback claim | Remains `new`/eligible | Poison row; repeated failure |
| Coordinator crash | supervisor/restart | Rebuild services | Rows remain | Full replay; old result loss |
| Worker misses heartbeat | pool observer | Delete worker only | None | Stuck started job; artifact loss |
| Heartbeat request fails | worker | Requeue result batch | None | Delayed duplicate batch |
| Heartbeat response lost | worker timeout/error | Retry results | Assignments not replayed | Lost dispatch |
| Source save fails on worker | worker | Log; enqueue job | None | Later input error |
| Artifact unavailable/near expiry | source callback | Error result, finish | Error finish if commit works | No replica/retry |
| Output save fails | worker | Log | Result may still say output | Panic/missing downstream file |
| Result DB/message/histogram fails | coordinator | Finish error | Finish may commit separately | Successful compute reported failed |
| Finish transaction fails | coordinator | Log after local removal | Remains scheduled | Full replay |
| Kafka write fails | dispatcher | Roll back, local backoff | Outbox remains | Failure count lost |
| Kafka commit uncertainty | dispatcher | Retry row | Outbox may remain | Duplicate Kafka record |
| Oldest execution too heavy | scheduler | Skip tick | Row unchanged | Blocks lighter rows |
| Promised job has no worker | job scheduler | Empty-worker promise | None | Orphan/starvation |
| Graceful signal | main | HTTP shutdown, process exit | DB/files as already committed | No drain/flush |

## Emitted messages/events

| Failure class | Business message | Scheduler event/log |
| --- | --- | --- |
| Internal job/source error | `finish` with `error` if finish commits | Job/finish logs/events |
| Domain CE/RE/TL/ML/WA | Job status then `finish` without internal error | Finished events |
| Worker removal | None | `removed` worker event/log |
| Coordinator restart/stale replay | New `start` and later repeated job/finish messages | New started/picked events |
| Kafka failure | History already committed | Dispatcher error log only |

## Observability

Logs and best-effort seven-day scheduler tables are the primary diagnostics.
The only custom metric is `now_weight`; it can itself leak or go negative after
races/errors. There are no recovery, retry-attempt, stuck-job, missing-worker,
artifact-loss, ignored-result, outbox-depth, or shutdown-drain metrics/alerts.

## Implementation references

- `Exesh/internal/scheduler/{execution_scheduler.go,job_scheduler.go,worker_pool.go}`
- `Exesh/internal/worker/worker.go`
- `Exesh/internal/dispatcher/message_dispatcher.go`
- `Exesh/internal/storage/postgres`
- `Exesh/internal/provider` and filestorage adapter
- `Exesh/cmd/{coordinator,worker}/main.go`

## Current guarantees

Committed PostgreSQL rows survive process restart according to PostgreSQL
durability. Workers retry result batches on observed heartbeat error. Stale
scheduled executions are eventually eligible for full replay if scheduler and
dependencies work. No current guarantee resumes at the failed job, prevents
overlap, retains artifacts, or emits messages exactly once.

## Open questions

Which failures should retry a job versus an execution? How are attempts fenced?
What is the worker-loss SLA? Which state must be durable? Is replay safe for all
consumer side effects? See [Open questions](open-questions.md).

## Proposed requirements

- Define an explicit attempt/job state machine, durable acknowledgements, leases,
  fencing, retry budgets, and terminal failure reasons.
- Couple worker loss to deterministic job recovery and artifact policy.
- Make state mutation transactional or compensatable across database boundaries.
- Add graceful draining and recovery observability/SLOs.
- Test every matrix row under restart and concurrency.

## Test coverage

- **Existing tests / covered scenarios:** no Exesh recovery tests; filestorage's
  tests do not cover orchestration or volatile-map loss.
- **Missing scenarios:** every failure-matrix row, all restart combinations,
  overlap, duplicates, and graceful shutdown.
- **Required integration tests:** multi-process coordinator/workers/PostgreSQL/
  filestorage/Kafka with durable assertions before and after restart.
- **Required failure-injection tests:** kill, pause, partition, timeout, corrupt
  response, expire files, and fail each database/broker boundary.

## Coordinator restart and stale replay

```mermaid
sequenceDiagram
    participant Old as Old coordinator
    participant W as Worker
    participant DB as PostgreSQL
    participant New as New coordinator

    Old->>DB: execution = scheduled
    Old-->>W: job attempt A
    Old--xOld: process exits; graph/started/artifacts lost
    W->>New: result A (unknown job ID in started map)
    New-->>W: HTTP OK; result ignored
    New->>DB: after retry threshold, claim scheduled row
    New->>New: rebuild graph from zero
    New-->>W: job attempt B with same deterministic job ID
    Note over DB,W: history/samples/artifacts may repeat
```
