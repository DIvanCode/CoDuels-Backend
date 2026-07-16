# Execution priority and resource estimates

## Purpose

Estimate admission weight and per-job resource demand, then order one ready job
from each active execution for worker placement.

## Participants

Calculator, category histogram storage, execute use case, execution factory,
execution scheduler wrapper, job scheduler, worker pool, and scheduler event
storage.

## Trigger

Estimates are loaded during submission and materialization. Priority is computed
whenever a worker heartbeat asks the job scheduler for work and no promised job
has already been selected.

## Preconditions

Job definitions should have meaningful category names and positive time/memory
limits. The current code does not enforce those conditions. Priority assumes a
non-null `scheduled_at` and a positive total expected time.

## Current behavior

For each unique non-empty category, PostgreSQL histograms yield sample count,
median, and maximum. Time observations use 50ms buckets represented by bucket
start plus 50; memory uses 16MB buckets plus 16. With no samples, expected time
and memory equal job limits. With samples:

`expectedTime = clamp((3 * maxTime + 7 * medianTime) / 10, 100, timeLimit)`

`expectedMemory = clamp((3 * maxMemory + 7 * medianMemory) / 10, 16, memoryLimit)`

If a limit is below the minimum, `clamp` first raises the maximum to the
minimum, so the estimate can exceed the submitted limit. Execution weight is
the sum of `expectedTime * expectedMemory` over all submitted definitions.

The scheduler wrapper computes total expected time by iterating `JobByName`;
duplicate names across stages collapse in that map. After each outer job/chain
completion it adds that job's expected time to done expected time. At a worker
request time `now`, exact priority is:

`gamma^(max(0, tries - 1)) * (alpha * expectedRest + progress)`

where `alpha = 10.3174`, `gamma = 1.31`,
`expectedRest = (totalExpected - doneExpected) / totalExpected`, and
`progress = (now - scheduledAt in milliseconds) / totalExpected`.

Executions are sorted descending. The job scheduler considers only the FIFO
head of each execution in that order. Despite the method name, `tries` in the
live object is the value loaded plus the current schedule call; durable `tries`
also increments on every recognized result, but those increments do not update
the live wrapper. Zero total time produces division by zero/NaN behavior rather
than a defined priority.

## State transitions

Histogram samples accumulate on recognized job results. `tries` moves
`0 -> 1 -> ...` on scheduling and durable completion refreshes. Progress moves
conceptually from `0` toward `1`, but can exceed `1` and is not persisted.
Priority is a derived instantaneous value, not state.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Category histograms | calculator storage | PostgreSQL histogram tables | Yes | PostgreSQL |
| Execution weight | execute use case | `Executions.weight` | Yes | PostgreSQL |
| Per-job estimates | materialized job | Coordinator heap | No | Recomputed from current histograms |
| Total/done expected time | scheduler wrapper | Coordinator heap | No | Wrapper counters |
| Tries | execution definition | PostgreSQL and copied heap value | Durable value yes | PostgreSQL; live copy can lag |
| Candidate priority | execution scheduler | Temporary map/event | Event may persist | Current calculation |

## Persistence and transaction boundaries

Submission reads histograms and saves weight in one transaction. Scheduling
loads histograms again while claiming the row, so per-job estimates can differ
from those that produced the persisted weight. Histogram increments occur in
the same transaction as job history and scheduled timestamp refresh. Candidate
priority events are asynchronous and outside that transaction.

## Idempotency and duplicate handling

Histogram increments are not idempotent by job/attempt; replayed or duplicate
accepted results add samples again. Whole-execution replay loads the latest
histograms and can change placement estimates while retaining old persisted
weight. Candidate events repeat for every worker request.

## Concurrency and races

Histogram upserts are safe PostgreSQL increments. Wrapper priority/progress
methods lock its mutex, but completion increments
`TotalDoneJobsExpectedTime` without that mutex. Concurrent completion and
priority reads are therefore a data race in Go terms. Multiple coordinators use
shared histograms but independent capacity, progress, and active attempts.

## Failure handling

Histogram read failure blocks submission or scheduling. Histogram update failure
causes an otherwise successful job completion transaction to fail and the
execution to be finished with an internal error. Invalid or negative limits can
produce zero/negative estimates or weight with no validation. No fallback is
used when PostgreSQL is unavailable.

## Emitted messages/events

| Output | Condition | Durable | Notes |
| --- | --- | --- | --- |
| `picked_candidate` | Queue head examined | Best effort | Includes priority/progress |
| Job event expected resources | promise/start/finish | Best effort | Estimated, not limits |
| Prometheus `now_weight` | Scrape | No | Admission weight, not actual usage |
| Business history | Not emitted by priority | N/A | Job result path emits it |

## Observability

Scheduler events record estimates, priority, progress, predicted finish, and
latency. There are no metrics for histogram sample counts, prediction error,
queue age, heavy-row blocking, NaN priority, or capacity rejection. Worker
Prometheus exposes only default Go/process metrics.

## Implementation references

- `Exesh/internal/calculator/calculator.go`
- `Exesh/internal/storage/postgres/category_histogram_storage.go`
- `Exesh/internal/scheduler/{execution.go,execution_scheduler.go,job_scheduler.go}`
- `Exesh/internal/domain/execution/category_stats.go`

## Current guarantees

For positive, valid limits and nonempty jobs, the formulas above are
deterministic for one histogram snapshot. Sorting is descending by calculated
priority, and at most one queued candidate per active execution is offered to
the placement scan.

## Open questions

Are category names intentionally user/task-specific, limiting sample reuse?
Should estimates ever exceed limits? Is replayed telemetry a valid new sample?
Should priority use attempt count, completed-job count, or age? See
[Open questions](open-questions.md).

## Proposed requirements

- Validate limits and explicitly define units and lower bounds.
- Persist the estimate snapshot or recompute admission weight consistently.
- Make sample updates idempotent per execution attempt/job.
- Define priority for empty/zero-time work and make progress race-free.
- Measure prediction error and starvation before tuning constants.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** histogram SQL/buckets, clamp edges, exact formula,
  sorting, zero totals, duplicate samples/names, and concurrent progress reads.
- **Required integration tests:** seed PostgreSQL histograms and assert admission,
  priority order, and scheduler event values.
- **Required failure-injection tests:** unavailable histogram storage, malformed
  limits, repeated results, and concurrency under the race detector.
