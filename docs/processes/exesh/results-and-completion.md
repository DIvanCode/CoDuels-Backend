# Results and execution completion

## Purpose

Recognize a worker result, release scheduler resources, persist statistics and
public job messages, unlock graph successors, and mark the execution finished.

## Participants

Worker/done-results heartbeat, heartbeat use case, worker pool, job scheduler,
execution scheduler and graph, category histogram storage, execution storage,
message factory/dispatcher, scheduler event recorder, REST/Kafka consumers.

## Trigger

A heartbeat reports a result for a job ID present in the coordinator's
`startedJobs`, or an internal scheduler/source error directly invokes a job or
execution failure callback.

## Preconditions

Coordinator memory must still contain the started job and execution object.
For normal completion, result type/status must be supported by message factory,
and chain inner result IDs must map to definitions. PostgreSQL must contain the
execution row.

## Current behavior

1. Job scheduler removes the recognized `startedJobs` entry and the worker-pool
   predicted allocation, records a best-effort `finished` event, then invokes
   the completion callback outside its mutex.
2. A result with `GetError() != nil` increments expected progress and immediately
   calls `finishExecution(error)`. It does not update the graph, category
   histograms, or emit a job-type business message.
3. A non-error result opens one transaction and locks the execution row `FOR
   UPDATE`. Chain results expand to their executed inner results; normal results
   form a one-element list.
4. For every result, it increments both category histograms and creates a
   type-specific history/outbox message. Compile supports OK/CE, run uses status
   and optional output, and check uses status. An unknown status/type is an
   internal error.
5. Still inside the transaction, the process-local graph marks the outer job
   done using the outer result status. It then calls `SetScheduled` on the
   separately loaded durable definition, incrementing `tries` and refreshing
   `scheduled_at`, saves it, and commits.
6. After commit, graph-ready jobs enter scheduler FIFOs. If the graph is done,
   `finishExecution(nil)` is called. A non-success job status cancels dependent
   jobs/stages but is not an execution error; completion eventually emits a
   successful empty-error `finish` message.
7. `finishExecution` first records a best-effort finish event, defers weight
   decrement, sets force-failed (also used as a terminal guard), and deletes the
   active map entry. Then one transaction creates a successful/error `finish`
   history/outbox message, mutates the in-memory definition to `finished`, and
   saves it.

The graph mutation occurs before its surrounding database transaction commits
and cannot be rolled back. A histogram/message/storage failure for a successfully
executed job therefore converts the execution into an internal-error finish.
The started entry was already removed, so redelivery is ignored.

## State transitions

Conceptual job: `started -> completed recognized -> graph done` or `started ->
internal error`. Durable execution: `scheduled -> scheduled` on a recognized
non-error result, then `scheduled -> finished`. Domain non-success statuses
(`CE`, `RE`, `TL`, `ML`, `WA`, or any status differing from configured success)
cancel successors but normally end the execution with finish message error empty.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Started recognition/callback | job scheduler | Coordinator heap | No | `startedJobs` |
| Worker predicted allocation | worker pool | Coordinator heap | No | `RunningJobs` |
| Graph completion/cancellation | execution graph | Coordinator heap | No | Graph maps/counters |
| Execution status/tries/time | execution storage | PostgreSQL | Yes | `Executions` |
| Category samples | histogram storage | PostgreSQL | Yes | Histogram tables |
| Job/finish history and outbox | dispatcher | PostgreSQL | Yes | `Messages` / `Outbox` |
| Completion events/weight | event recorder/scheduler | Async DB / heap | Partly | Telemetry / atomic counter |

## Persistence and transaction boundaries

For non-error results, histogram increments, all job messages, and execution
timestamp save share one transaction. The graph mutation is in-memory inside the
callback and not rollback-safe. Successor enqueue is after commit. Finish uses a
separate transaction. Active-map deletion, force flag, event emission, and
weight decrement are outside/before finish commit; a failure leaves a durable
scheduled row that later replays.

## Idempotency and duplicate handling

The first result removes `startedJobs`; later duplicates are ignored in that
process. This protects history from simple heartbeat redelivery after a fully
successful callback, but also prevents recovery after callback failure. A
coordinator restart loses the recognition map and ignores all old results.
Whole-execution replay creates fresh samples/messages and can finish again.
Concurrent terminal callbacks are not atomically deduplicated.

## Concurrency and races

Different results can invoke callbacks concurrently. Graph methods are locked,
but expected-progress increments are not; terminal check/set spans separate
locks. Two callbacks can both observe nonterminal, both call finish, decrement
weight twice, and attempt duplicate finish messages. PostgreSQL execution-row
and message-sequence locks serialize database work but do not fence stale
attempts or in-memory objects.

## Failure handling

Result error finishes execution with its error string. Histogram, definition
lookup, message construction/insertion, or save error is logged and converted
to an internal-error finish. If finish persistence fails, only a log remains and
stale replay is expected. A message factory error for an unexpected worker
status also makes successful computation an execution error. No compensating
job requeue or durable dead-letter status exists.

## Emitted messages/events

| Type | Payload | Condition | Durable |
| --- | --- | --- | --- |
| `compile` | execution ID, job name, OK/CE, optional compilation error | Each executed compile result | History; optional outbox |
| `run` | execution ID, job name, status, optional output | Each executed run result | History; optional outbox |
| `check` | execution ID, job name, status | Each executed check result | History; optional outbox |
| `finish` | execution ID, optional internal error | Terminal path | History; optional outbox |
| `finished` job/execution event | estimates/actuals or finish status | Before callbacks/commit | Best effort telemetry |

`start` is emitted by scheduling, not completion. A chain can emit several job
messages in one transaction but only for inner results actually executed.

## Observability

Logs identify job/execution IDs and internal errors. Scheduler events capture
actual duration and finish progress/status. History exposes product results.
There are no counters for ignored/duplicate results, canceled jobs, persistence-
induced failure, double finish, or retry attempt identity.

## Implementation references

- `Exesh/internal/scheduler/{job_scheduler.go,execution_scheduler.go}`
- `Exesh/internal/domain/execution/graph.go`
- `Exesh/internal/factory/message_factory.go`
- `Exesh/internal/storage/postgres/{execution_storage.go,category_histogram_storage.go}`
- `Exesh/internal/domain/execution/result` and `message`

## Current guarantees

Within a successful non-error completion transaction, its histogram updates,
job history/outbox rows, and scheduled timestamp commit together. Per-execution
message IDs increase under an execution-row lock. A recognized started job gets
at most one callback in one live job scheduler. These do not imply exactly-once
computation or completion across failure/restart.

## Open questions

Should telemetry/history/storage failure change a verdict into execution error?
Should a domain failure finish successfully? Which inner chain messages are
contractually required? How is terminal idempotency defined? See
[Open questions](open-questions.md).

## Proposed requirements

- Persist result/attempt receipt before removing recoverability and make apply
  idempotent by attempt/job.
- Separate domain verdict from infrastructure completion status.
- Keep graph mutation after commit or persist a resumable graph/job ledger.
- Make terminal transition a single fenced compare-and-set.
- Define job messages for internal errors and canceled jobs if consumers need them.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** recognition, graph unlock/cancel, chains, atomicity,
  duplicates, concurrent finish, and status/message consistency.
- **Required integration tests:** real PostgreSQL result batches and graph
  progress through every verdict and successful/error finish.
- **Required failure-injection tests:** fail histogram/message/save/commit at
  each point, redeliver results, and race terminal callbacks.
