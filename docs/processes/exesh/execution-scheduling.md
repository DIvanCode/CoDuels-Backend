# Execution scheduling

## Purpose

Claim persisted executions within a process-local weight budget, materialize
them, emit their start, expose ready jobs, refresh stale timestamps on results,
and persist final completion.

## Participants

Execution scheduler, PostgreSQL execution storage and unit of work, execution
factory, coordinator filestorage, message dispatcher, job scheduler, worker
pool, category histograms, and scheduler event recorder.

## Trigger

Coordinator startup starts one goroutine. Each configured `executions_interval`
(500ms in checked-in config) initiates a scheduling attempt. A job result can
schedule successors or finish an active execution.

## Preconditions

Remaining process-local capacity must be positive. PostgreSQL must contain a
`new` row or a `scheduled` row with `scheduled_at` older than
`execution_retry_after` (30s). Its weight must fit the remaining capacity, and
factory materialization must succeed.

## Current behavior

1. A tick computes `capacity - nowWeight`. `nowWeight` is an atomic integer in
   one coordinator process.
2. One transaction selects the oldest eligible row using `FOR UPDATE SKIP
   LOCKED`. Only one row is examined.
3. If that row is too heavy, the tick ends without considering later lighter
   rows. A permanently oversized oldest row blocks all following rows.
4. The factory rebuilds the entire execution. The scheduler stores it in an
   in-memory map, calls `SetScheduled` (incrementing `tries`), increments
   `nowWeight`, records a best-effort `started` event, inserts the durable
   history/outbox `start`, and enqueues graph roots.
5. The updated definition is saved and the transaction commits. Start history,
   optional outbox, and status are atomic with this commit; scheduler events and
   filesystem downloads are not.
6. Each accepted job completion runs another transaction. It updates category
   histograms, inserts job messages, mutates the process-local graph, loads the
   durable row `FOR UPDATE`, calls `SetScheduled` again, and saves it. This
   increments durable `tries` for job completion as well as scheduling and
   refreshes `scheduled_at`.
7. Ready successors are enqueued after commit. When the graph reports done, or
   an internal result/persistence error occurs, `finishExecution` removes the
   execution from memory and decrements weight before a transaction inserts a
   finish message and saves `status = finished`.

A stale retry rebuilds and reruns the whole definition from zero. It can occur
while an earlier run or long job is still alive because `scheduled_at` is a
completion-refreshed lease without owner identity or fencing. The same
coordinator does not exclude IDs already in its map and can overwrite the map
entry; multiple coordinators have separate maps and capacity counters.

## State transitions

Durable: `Execution: new -> scheduled -> finished`; stale replay performs
`scheduled -> scheduled` and increments `tries`. Each recognized successful
job-result transaction also performs `scheduled -> scheduled` and increments
`tries`. In memory: `absent -> active -> removed`; a replay creates another
active object even when earlier callbacks still reference the previous one.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Definition/status/tries/timestamps | execution storage | `Executions` | Yes | PostgreSQL |
| Active execution and graph progress | execution scheduler | `executions` map | No | Process memory |
| Ready FIFO | scheduler execution wrapper | Process memory | No | Queue head |
| Capacity usage | execution scheduler | atomic `nowWeight` | No | Current process counter |
| Started/promise/worker state | job scheduler/worker pool | Process memory | No | Respective maps/slice |
| History/outbox | dispatcher | PostgreSQL | Yes | `Messages` / `Outbox` |
| Scheduler events | recorder | Async PostgreSQL tables | Yes after insert | Best-effort telemetry |

## Persistence and transaction boundaries

Claim/materialize/start/status-save share a PostgreSQL transaction and row lock;
local source downloads and map/counter mutations do not roll back. Result
application has its own transaction. Finish history and finished status share a
transaction, but map deletion and weight decrement occur before it. If that
transaction fails, the durable row remains scheduled and will replay; the local
run is already discarded. There is no persisted coordinator owner, lease token,
job ledger, graph checkpoint, or capacity reservation.

## Idempotency and duplicate handling

`FOR UPDATE SKIP LOCKED` prevents two transactions from claiming the same row at
the same instant, not later stale overlap. Start/job/finish messages have no
semantic deduplication key. Whole-execution replay reuses job IDs but creates new
history IDs, may repeat computation, and may reuse/overwrite worker artifacts.
Map overwrite and callbacks from overlapping runs are not fenced by attempt.

## Concurrency and races

An execution-scheduler mutex protects the active map; `nowWeight` is atomic;
graph internals use a mutex. `finishExecution` checks `IsForceFailed` and sets
force-failed in separate critical sections, so concurrent terminal callbacks
can both pass the check, delete, emit, and decrement. `TotalDoneJobsExpectedTime`
is updated outside its wrapper mutex. A stale retry may overwrite `executions[id]`
while old job callbacks still operate. Capacity is not global across coordinator
instances.

## Failure handling

Scheduling errors are logged and the weight added in that attempt is subtracted.
The in-memory map entry is not cleaned up on every scheduling error. A source
download can therefore leave filesystem data; an error after enqueue can leave
an orphan object/queue. A finish transaction error is logged only; stale replay
is the implicit recovery. There is no maximum tries or terminal poison status.

## Emitted messages/events

| Message/event | Condition | Durable | Delivery |
| --- | --- | --- | --- |
| `start` history/outbox | Scheduling attempt reaches dispatcher | Yes on commit | REST history; Kafka if enabled |
| `started` scheduler event | Before start send | Best effort | Async event table |
| `picked_candidate` scheduler event | Active execution exposes queue head | Best effort | Async event table |
| `finish` history/outbox | Terminal transaction | Yes on commit | Can repeat after replay/race |
| `finished` scheduler event | Before terminal transaction | Best effort | Can exist even if transaction fails |

## Observability

The only custom Prometheus metric is
`coduels_exesh_coordinator_now_weight`. Logs cover scheduling, capacity skips,
jobs, and finish. Scheduler event tables capture candidate priority/progress and
finish status/duration with seven-day retention. They can be dropped when the
10,000-item channel is full and are not transactionally aligned with state.

## Implementation references

- `Exesh/internal/scheduler/{execution_scheduler.go,execution.go}`
- `Exesh/internal/storage/postgres/execution_storage.go`
- `Exesh/internal/factory/execution_factory.go`
- `Exesh/internal/domain/execution/execution_definition.go`
- `Exesh/cmd/coordinator/main.go`
- `Exesh/config/coordinator.yml`

## Current guarantees

Within one claim transaction, PostgreSQL row locking prevents a simultaneous
claim of that row. On a successful commit, start history and scheduled status
are durable together. Within one live execution object, graph methods serialize
dependency progress. These do not guarantee a single execution attempt,
durable job progress, global capacity, or exactly-once finish.

## Open questions

Is `scheduled_at` a lease or activity timestamp? What should `tries` count?
Should stale runs be fenced/canceled? May multiple coordinators be deployed?
Should a heavy head-of-line row block lighter work? See
[Open questions](open-questions.md).

## Proposed requirements

- Persist an attempt/lease owner and fencing token with explicit renewal.
- Separate scheduling-attempt count from job progress and activity timestamps.
- Skip oversized rows or give them a terminal/admission outcome.
- Make finish claim atomic and idempotent per attempt.
- Define global capacity behavior before allowing multiple coordinators.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** claim SQL, stale overlap, capacity/head-of-line behavior,
  multi-coordinator scheduling, concurrent finish, restart, and weight accounting.
- **Required integration tests:** real PostgreSQL row locks with two schedulers
  and long executions across the retry threshold.
- **Required failure-injection tests:** fail every step after map/weight mutation,
  fail finish commit, and kill/restart coordinator mid-attempt.
