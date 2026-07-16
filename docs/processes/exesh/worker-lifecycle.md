# Worker lifecycle

## Purpose

Start worker loops, register worker capacity through heartbeat, track predicted
running jobs/artifacts, detect missed heartbeats, and stop the process.

## Participants

Worker binary, worker heartbeat loop, coordinator heartbeat API, worker pool and
observer, job scheduler, worker filestorage, HTTP/metrics servers, and operating
system/container supervisor.

## Trigger

Worker/coordinator application startup; recurring 100ms heartbeat; recurring
worker-pool observer tick (configured to one second); SIGINT/SIGTERM; or missed
heartbeats.

## Preconditions

Worker config supplies a stable HTTP endpoint-like ID, positive slots/memory,
coordinator endpoint, and writable filestorage. Network access to the
coordinator and between workers is required for artifact transfer.

## Current behavior

1. A worker starts filestorage HTTP routes, one heartbeat goroutine, and one
   execution goroutine per configured slot. It exposes default Go/process
   metrics only.
2. Its first heartbeat registers ID, normalized total slots/memory, empty
   artifacts/running jobs, and current timestamp. Later heartbeats update only
   `LastHeartbeat`; they do not change registered totals.
3. The coordinator pool records predicted jobs placed/removed by its scheduler
   and artifact expiry timestamps reported with results. It does not ingest the
   worker's actual job list.
4. The observer wakes every `worker_die_after`. If
   `LastHeartbeat + worker_die_after < now`, it deletes the worker registry
   entry and records `removed`.
5. Deletion also discards that worker's advertised artifact locations and
   predicted running map. It does not inspect or modify job scheduler
   `startedJobs` or `promisedJobs`, does not fail executions, and does not send a
   cancellation to the worker.
6. A later heartbeat with the same ID creates a fresh empty worker entry.
   Previously started jobs are still present in the job scheduler and a late
   result can be accepted by job ID, but pool bookkeeping may refer to a new
   registration.
7. On signal, HTTP servers are shut down with the still-live root context.
   Root cancellation is deferred until `main` returns; background loops have no
   wait group, no job drain, and no explicit result flush. Filestorage shutdown
   is deferred.

## State transitions

Conceptual worker transitions: `unknown -> registered -> heartbeat-active ->
removed`; a later heartbeat performs `removed -> registered`. Worker process:
`starting -> serving/executing -> process exit`. There is no persisted draining,
dead, or recovered state.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Configured ID/capacity | worker config | YAML/environment | Yes as deployment config | Process config |
| Registry/last heartbeat | worker pool | Coordinator heap | No | Pool map |
| Predicted running jobs/artifacts | worker pool | Coordinator heap | No | Pool entry |
| Started jobs/promises | job scheduler | Coordinator heap | No | Scheduler maps |
| Local queue/counters/done results | worker | Worker heap | No | Worker mutex-protected state |
| Cached bytes/artifacts | worker filestorage | Local filesystem | Only if root persists; production does not mount it | Local storage |

## Persistence and transaction boundaries

Lifecycle state never enters PostgreSQL. Worker/scheduler events are queued
asynchronously to PostgreSQL but cannot restore control state. Registration and
removal are mutex-protected map operations. Container restart recreates worker
heap; coordinator restart forgets every worker until the next heartbeat.

## Idempotency and duplicate handling

Repeated heartbeats safely refresh time for an existing ID. Re-registration
with the same ID resets pool state. IDs are not authenticated, so two processes
using one ID overwrite/shared-update one entry while sending independent queues
and results. Removal is idempotent at the map level but has no recovery side
effect.

## Concurrency and races

The pool mutex protects registry, running jobs, and artifacts. The observer can
delete a worker between the heartbeat registration call and later result/artifact
processing in the same HTTP request; `PutArtifact`, `placeJob`, and `removeJob`
dereference the map entry without a nil check. With a one-second death threshold,
slow request processing can therefore panic. Worker scheduler and worker local
counters are distinct and can diverge.

## Failure handling

Network loss causes worker results to be requeued locally, but after coordinator
removal there is no job recovery. Worker process death loses queued jobs and
unacknowledged results. Coordinator death loses its registry and started jobs;
workers continue retrying results until a new coordinator responds, which no
longer recognizes them. Graceful shutdown does not guarantee job completion or
result delivery.

## Emitted messages/events

| Event | Condition | Durable | Notes |
| --- | --- | --- | --- |
| `registered` worker event | Unknown ID heartbeat | Best effort | Initial totals |
| `heartbeat` worker event | Every heartbeat | Best effort | Coordinator-predicted free values |
| `removed` worker event | Missed-heartbeat observer | Best effort | Not recovery |
| `job_placed` / `job_removed` | Scheduler accounting | Best effort | Predicted, process-local |
| Business message | None for lifecycle | N/A | No worker-death message |

## Observability

Coordinator logs registration/removal and writes high-volume heartbeat events.
Worker logs heartbeat errors and job start/done at debug level. No custom worker
metrics expose queue, running jobs, results pending, artifact count, last
successful heartbeat, or graceful drain. Event retention is seven days.

## Implementation references

- `Exesh/cmd/{worker,coordinator}/main.go`
- `Exesh/internal/worker/worker.go`
- `Exesh/internal/scheduler/worker_pool.go`
- `Exesh/internal/storage/postgres/scheduler_event_storage.go`
- `Exesh/config/worker.yml` and `coordinator.yml`
- `Exesh/ansible/deploy/playbook.yml`

## Current guarantees

A live coordinator serializes pool mutations and expires registrations after
the configured missed-heartbeat threshold. A worker retries the batch of results
when its heartbeat call returns an error. Removal itself guarantees only that
the pool will no longer select that entry or advertise its artifacts.

## Open questions

What is the supported identity/authentication model? Must started jobs be
requeued, failed, or allowed to return after removal? What shutdown/drain SLO is
required? Is one-second death detection safe under storage latency? See
[Open questions](open-questions.md).

## Proposed requirements

- Introduce worker sessions/epochs and authenticate IDs.
- Define a persisted or reconstructable recovery action for every started job.
- Use a lease duration tolerant of heartbeat processing/network latency.
- Add draining and bounded graceful shutdown with final result acknowledgement.
- Guard every pool lookup against concurrent removal.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** registration/capacity, missed heartbeat, removal races,
  re-registration/ID collision, restart, and graceful shutdown.
- **Required integration tests:** real coordinator plus workers through register,
  execute, drain, stop, restart, and re-register sequences.
- **Required failure-injection tests:** pause network beyond death threshold,
  kill worker with queued/running/result-pending work, and slow result handling.

## Worker death and current non-recovery

```mermaid
sequenceDiagram
    participant W as Worker
    participant P as Worker pool
    participant JS as Job scheduler
    participant ES as Execution scheduler

    W->>P: heartbeat; job is predicted running
    Note over JS: startedJobs keeps callback
    W--xP: heartbeats stop
    P->>P: delete worker, running map, artifact map
    Note over JS,ES: no notification, requeue, fail, or timeout
    Note over ES: execution and nowWeight can remain active forever
```
