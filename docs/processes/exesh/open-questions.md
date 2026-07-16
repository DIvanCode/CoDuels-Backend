# Exesh open questions

These entries separate observed behavior from decisions. “Possible options” are
not approved requirements.

## OQ-01: Chain runtime can weaken isolation

### Current behavior

The chain runtime is selected from the first inner job and reused for every
inner executor. A compile-first chain therefore uses local runtime for later
run/check jobs that would use isolate when standalone.

### Why this is a problem

The current Duely C++/Go linear compile/run graph is chain-eligible, so an
untrusted binary can execute in the worker container rather than isolate.

### Affected files

- `Exesh/internal/domain/execution/reduce_chain_jobs.go`
- `Exesh/internal/executor/executors/chain_job_executor.go`
- `Exesh/cmd/worker/main.go`

### Questions

- May jobs with different isolation classes ever share a chain/runtime?
- Is compile-local itself an accepted trust boundary?

### Possible options

- Forbid cross-policy chains.
- Create separate runtimes at policy boundaries.
- Run every chain in the strongest required isolation.

## OQ-02: Stale execution retry overlaps live work

### Current behavior

A scheduled row older than 30 seconds is rebuilt from zero. There is no lease
owner/fence and no check that the same coordinator already has it active.

### Why this is a problem

Long jobs, coordinator pauses, or multiple coordinators can run the same job IDs
concurrently, overwrite active maps/artifacts, duplicate samples/messages, and
corrupt `nowWeight` accounting.

### Affected files

- `Exesh/internal/scheduler/execution_scheduler.go`
- `Exesh/internal/storage/postgres/execution_storage.go`
- `Exesh/internal/domain/execution/execution_definition.go`

### Questions

- Is `scheduled_at` a lease, last progress, or retry timestamp?
- Should retry cancel/fence an older attempt?
- What should `tries` count?

### Possible options

- Persist attempt ID, owner, lease, heartbeat, and fencing token.
- Persist job progress and retry only missing jobs.
- Disallow multi-coordinator scheduling until ownership exists.

## OQ-03: Worker removal does not recover started jobs

### Current behavior

The observer deletes a worker pool entry and artifact map. Job scheduler
`startedJobs` and promises are untouched and have no timeout.

### Why this is a problem

Executions and capacity can remain stuck, and downstream artifacts are lost.
Re-registration creates an empty pool entry without reconciling old work.

### Affected files

- `Exesh/internal/scheduler/worker_pool.go`
- `Exesh/internal/scheduler/job_scheduler.go`

### Questions

- Should lost jobs fail, requeue, or await a session-bound late result?
- What retry budget and artifact policy apply?

### Possible options

- Emit worker-loss callbacks for every allocation.
- Persist dispatch leases and requeue after expiry.
- Replicate artifacts before unlocking dependents.

## OQ-04: Successful computation can become infrastructure failure

### Current behavior

Histogram updates, job history, graph mutation, and durable timestamp refresh
are coupled. Any storage/message error leads to an error finish after the
started result was removed.

### Why this is a problem

The caller can receive an infrastructure error despite a valid verdict, graph
progress cannot roll back, and the result cannot be reapplied.

### Affected files

- `Exesh/internal/scheduler/execution_scheduler.go`
- `Exesh/internal/storage/postgres/category_histogram_storage.go`
- `Exesh/internal/dispatcher/message_dispatcher.go`

### Questions

- Which data is required for accepting a result?
- Are histograms/telemetry allowed to fail the execution?

### Possible options

- Persist an idempotent result ledger first.
- Make histogram collection asynchronous/non-fatal.
- Separate domain verdict from infrastructure status.

## OQ-05: Output failure can advertise a missing artifact

### Current behavior

The worker logs `SaveOutput` errors without clearing `HasOutput` or returning an
error. Heartbeat dereferences `artifact_trash_time` for every output.

### Why this is a problem

Coordinator processing can panic or later dispatch a non-existent artifact.
`OutputProvider` also checks the wrong captured error in commit/abort branches.

### Affected files

- `Exesh/internal/worker/worker.go`
- `Exesh/internal/provider/output_provider.go`
- `Exesh/internal/executor/executors/*_job_executor.go`
- `Exesh/internal/usecase/heartbeat/usecase.go`

### Questions

- Is artifact commit required for job success?
- How is commit uncertainty represented?

### Possible options

- Convert save failure to typed internal result error.
- Validate non-null expiry before heartbeat mutation.
- Make artifact commit idempotent by attempt and checksum.

## OQ-06: Artifact has one volatile location

### Current behavior

Artifacts live on the producing worker with five-minute TTL. Locations live only
in coordinator memory, and entries within one minute of expiry are discarded.

### Why this is a problem

Worker/coordinator restart or expiry permanently breaks downstream dependencies;
there is no replica, renewal, or discovery.

### Affected files

- `Exesh/internal/scheduler/worker_pool.go`
- `Exesh/internal/scheduler/execution_scheduler.go`
- `Exesh/internal/provider/output_provider.go`

### Questions

- How long must an artifact remain available?
- Who owns replication and cleanup acknowledgement?

### Possible options

- Store artifacts centrally/durably.
- Persist replicated locations and pin until dependents finish.
- Recompute upstream jobs under a fenced retry policy.

## OQ-07: Heartbeat has no partial acknowledgement or dispatch ack

### Current behavior

An OK heartbeat acknowledges the entire result batch and simultaneously returns
new jobs. Unknown results are silently ignored; response loss loses assignments
while results are retried.

### Why this is a problem

There is no way to distinguish accepted/rejected results or safely replay both
directions. Worker/source/session identity is not fenced.

### Affected files

- `Exesh/internal/api/heartbeat`
- `Exesh/internal/usecase/heartbeat/usecase.go`
- `Exesh/internal/worker/worker.go`

### Questions

- What exactly does heartbeat OK acknowledge?
- When is a dispatch considered owned/running?

### Possible options

- Add heartbeat sequence and per-result acknowledgements.
- Separate dispatch leasing from heartbeat.
- Cache/replay responses by worker session and sequence.

## OQ-08: Oldest heavy execution blocks all lighter work

### Current behavior

The scheduler selects one oldest eligible row. If its weight exceeds remaining
or total capacity, it does not query another row.

### Why this is a problem

One oversized execution can starve every later execution forever.

### Affected files

- `Exesh/internal/storage/postgres/execution_storage.go`
- `Exesh/internal/scheduler/execution_scheduler.go`

### Questions

- Is strict FIFO required?
- What is the outcome for work larger than total capacity?

### Possible options

- Select the oldest fitting row.
- Admit a bounded over-capacity head.
- Reject/dead-letter impossible definitions at submission.

## OQ-09: Promises can become orphaned

### Current behavior

An impossible job is removed from the execution FIFO and promised with empty
worker ID and a predicted time one minute later. Promises are memory-only and
have no expiry.

### Why this is a problem

The job can starve invisibly; worker death/restart does not trigger explicit
recovery; coordinator restart loses the reservation.

### Affected files

- `Exesh/internal/scheduler/job_scheduler.go`

### Questions

- Is a promise binding or advisory?
- When should impossible work fail?

### Possible options

- Keep impossible jobs in a visible queue with reason.
- Add promise expiry/requeue.
- Validate worker-class feasibility before admission.

## OQ-10: Message and outbox delivery semantics are incomplete

### Current behavior

History is durable; outbox exists only with Kafka enabled. Kafka write occurs
inside a DB transaction, can duplicate after commit uncertainty, and failed
retry metadata is rolled back. The oldest row blocks later rows.

### Why this is a problem

Exactly-once is not provided, retry visibility is inaccurate, and a poison row
can stop the topic. Consumers need explicit deduplication rules.

### Affected files

- `Exesh/internal/dispatcher/message_dispatcher.go`
- `Exesh/internal/storage/postgres/{message_storage.go,outbox_storage.go}`

### Questions

- Is REST history or Kafka canonical?
- What stable event identity and ordering must consumers honor?

### Possible options

- Commit failure state separately and add dead-letter handling.
- Use `SKIP LOCKED` with per-stream ordering constraints.
- Version schemas and require consumer dedupe by event/outbox ID.

## OQ-11: Empty, cyclic, or invalid graphs have no terminal policy

### Current behavior

Submission omits semantic graph validation. Empty active stages never finish;
cycles/missing dependencies can yield no roots; cross-stage duplicate job names
collide; artifact references are order-sensitive.

### Why this is a problem

Definitions can be accepted yet stick/replay/panic later, potentially blocking
the scheduler.

### Affected files

- `Exesh/internal/usecase/execute/usecase.go`
- `Exesh/internal/factory/execution_factory.go`
- `Exesh/internal/domain/execution/graph.go`

### Questions

- What graph forms and reference directions are supported?
- Which failures are caller errors versus terminal execution errors?

### Possible options

- Validate/topologically sort before persistence.
- Enforce global unique names and compatible sources.
- Persist an invalid terminal status for legacy poison rows.

## OQ-12: Resource models can diverge

### Current behavior

Coordinator placement uses predicted memory and registered totals; response
count uses worker-reported free slots, but reported available memory is not
enforced by immediate placement. Existing worker totals never update. Actual
jobs can use up to memory limit, greater than estimate.

### Why this is a problem

Workers may be overcommitted, predictions become stale, and multi-coordinator
capacity is independent.

### Affected files

- `Exesh/internal/scheduler/{job_scheduler.go,worker_pool.go}`
- `Exesh/internal/calculator/calculator.go`
- `Exesh/internal/worker/worker.go`

### Questions

- Which resource report is authoritative?
- Should placement use expected, limit, or a safety factor?

### Possible options

- Reconcile acknowledged worker snapshots.
- Enforce both reported availability and predicted reservations.
- Add cgroup/isolate measurements and adaptive safety margins.

## OQ-13: Terminal and weight operations are race-prone

### Current behavior

Terminal check and force-fail set are separate; concurrent callbacks can both
finish. Map deletion and weight decrement occur before finish commit. Scheduling
errors can leave map entries.

### Why this is a problem

Duplicate finish/history and negative/leaked `nowWeight` can distort capacity;
DB and heap can disagree.

### Affected files

- `Exesh/internal/scheduler/execution_scheduler.go`

### Questions

- What single operation owns terminal transition?
- How is capacity reconciled after restart/error?

### Possible options

- Atomic per-attempt terminal compare-and-set.
- Derive capacity from persisted active leases.
- Centralize cleanup in a defer/compensation path.

## OQ-14: Graceful shutdown does not drain work

### Current behavior

HTTP shutdown uses a live root context; cancellation is deferred until return.
Background loops and worker jobs are not awaited, results/outbox are not flushed,
and the Kafka writer/database are not explicitly closed.

### Why this is a problem

Planned restart behaves like a crash for process-local control state and can
lose assignments/results while accepting no more HTTP.

### Affected files

- `Exesh/cmd/coordinator/main.go`
- `Exesh/cmd/worker/main.go`

### Questions

- What drain deadline and ownership transfer are required?
- Should workers stop accepting jobs before final heartbeats?

### Possible options

- Add draining state, cancel, wait groups, final acknowledgements, and bounded
  close of dispatcher/storage/filestorage.
- Coordinate rolling restart through leases/sessions.
