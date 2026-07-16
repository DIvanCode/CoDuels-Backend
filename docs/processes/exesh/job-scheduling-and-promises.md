# Job scheduling and promises

## Purpose

Choose jobs for a requesting worker while respecting predicted slots/memory and
protecting predicted start times of a bounded set of waiting jobs.

## Participants

Heartbeat use case, job scheduler, execution scheduler, worker pool, ready-job
queues, source callbacks, scheduler event recorder, and the requesting worker.

## Trigger

Every accepted worker heartbeat calls `PickJobs(workerID, freeSlots,
availableMemory)` after processing all reported results.

## Preconditions

The worker must have registered in the process-local pool during the same
heartbeat. At least one execution must expose a ready FIFO head or the promise
list must contain a startable job. Source resolution must succeed before the job
can be returned.

## Current behavior

1. `PickJobs` loops at most the worker-reported free slot count. The reported
   available memory is decremented after each pick but is not used by
   `pickJob`/`canStartNowOnWorker`; immediate feasibility uses the worker pool's
   predicted allocations instead.
2. Under the job-scheduler mutex, all promises are temporarily removed. The
   first promise that can start on the requesting worker without delaying the
   current promise schedule is started. Its recorded `PromisedWorkerID` and
   `PromisedStartAt` are not strict affinity or not-before constraints.
3. Every 100ms, remaining promises are recomputed against current workers and
   predicted running jobs.
4. If no promise starts, the execution scheduler returns one queue head per
   active execution in descending priority. The first immediately feasible job
   is dequeued through `OnStart`, placed in `startedJobs`, and recorded in the
   worker pool.
5. A non-feasible candidate is promised while fewer than five promises exist.
   `OnStart` dequeues it immediately, so it exists only in the promise slice.
6. `getBestPromise` builds per-worker start/end scanline events from expected
   running and promised intervals and finds the earliest slot/memory window.
   If none exists it returns empty worker ID and `now + 1 minute`; the job is
   still promised and removed from its execution queue.
7. After unlocking, the selected job's source callback resolves artifact
   locations. On error, `DoneJob` synthesizes an error result, removes the
   started allocation, invokes execution failure, and recursively seeks another
   job for the same heartbeat.

Worker resource accounting uses expected memory, not job memory limits or
actual RSS. The worker-reported free slots bound response count, but an existing
worker's declared total slots/memory are not updated on later heartbeats.

## State transitions

Conceptual transitions: `ready -> promised -> started -> completed`; or
`ready -> started -> completed`. A promise can be rescheduled repeatedly or
started by another worker. On source-resolution failure:
`started -> internal error -> execution finish`. None are persisted job states.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Ready FIFO heads | execution wrapper | Coordinator heap | No | Per-execution queue |
| Promise list and predicted times | job scheduler | Coordinator heap | No | `promisedJobs` |
| Started job/callback/worker | job scheduler | Coordinator heap | No | `startedJobs[jobID]` |
| Predicted worker allocations | worker pool | Coordinator heap | No | `RunningJobs` map |
| Actual local queue/running capacity | worker | Worker heap | No | Worker counters/queue |
| Job limits/estimates | materialized job | Coordinator and response JSON | No | Current job object |

## Persistence and transaction boundaries

Placement and promises do not use PostgreSQL. Dequeue, promise, started map, and
worker-pool placement are memory-only operations under separate mutexes. Source
resolution can perform no database transaction but reads coordinator/worker
artifact registry and constructs HTTP source descriptors. Dispatch becomes real
only when the heartbeat response reaches and is accepted by the worker; no
durable dispatch/ack row bridges that boundary.

## Idempotency and duplicate handling

`startedJobs` is keyed only by deterministic job ID; a second allocation can
overwrite the first. `WorkerPool.placeJob` replaces an allocation of the same ID
and adjusts predicted memory. There is no worker-side job deduplication, attempt
number, or dispatch token. A promise is removed from the execution queue, so a
lost/orphan promise is not independently discoverable.

## Concurrency and races

The job scheduler serializes picks, promises, and started-map changes with one
mutex. It calls worker-pool methods that take a second mutex. Completion removes
the started entry under the job mutex but invokes its callback after unlocking.
Worker removal takes only the worker-pool mutex and does not notify the job
scheduler, so started and promised entries can refer to missing workers. Map
iteration makes ties nondeterministic.

## Failure handling

No-worker/no-window produces an orphan-like promise with empty predicted worker,
not a rejection. There is no timeout for promised or started jobs. Worker death
does not requeue them. Source resolution failure is terminal for the execution,
not a retry on a different artifact replica. If the heartbeat response is lost,
coordinator state still says started while the worker never received the job.

## Emitted messages/events

| Event | Condition | Durable | Notes |
| --- | --- | --- | --- |
| `promised` job event | Candidate reserved | Best effort | Predicted worker/start |
| `started` job event | Ready job starts | Best effort | Predicted memory interval |
| `promised_started` job event | Promise starts | Best effort | Actual requesting worker |
| `job_placed` worker event | Pool allocation | Best effort | Predicted totals |
| Business message | None at placement | N/A | Emitted only on recognized result |

## Observability

Events expose expected duration/memory, promise latency, worker, and memory
offset. There are no durable queue/promise inspection endpoints or metrics for
queue length, promise age, starvation, source-resolution failure, or dispatch
loss. Logs report inability to find a promise worker.

## Implementation references

- `Exesh/internal/scheduler/{job_scheduler.go,job.go,execution_scheduler.go,worker_pool.go}`
- `Exesh/internal/lib/queue`
- `Exesh/internal/usecase/heartbeat/usecase.go`
- `Exesh/config/coordinator.yml`

## Current guarantees

Inside one live coordinator, job-scheduler mutation is serialized. The checked
feasibility model does not exceed registered predicted slots/memory and refuses
an immediate placement that would move an existing predicted promise later.
This is a prediction guarantee only, not an actual-resource or delivery
guarantee.

## Open questions

Are promises reservations, priorities, or hints? May any worker steal them?
Should reported available memory constrain each assignment? How are jobs larger
than every worker handled? What recovers orphan promises and started jobs? See
[Open questions](open-questions.md).

## Proposed requirements

- Give each dispatch an attempt/token and persist or reconstruct its lifecycle.
- Define promise expiry, ownership, reassignment, and impossible-job handling.
- Reconcile worker-reported and coordinator-predicted resources explicitly.
- Use memory limits or a documented safety factor for admission.
- Ensure loss of a heartbeat response has a safe retry/ack protocol.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** scanline/equal-time placement, promise protection and
  rescheduling, impossible jobs, reported-memory divergence, and duplicates.
- **Required integration tests:** several heartbeat-driven workers with
  overlapping durations/memory and observable promise/start order.
- **Required failure-injection tests:** worker loss with promised/started work,
  source callback failure, lost dispatch response, and duplicate allocation.
