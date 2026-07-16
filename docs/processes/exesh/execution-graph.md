# Execution graph materialization

## Purpose

Turn the durable source/stage/job definitions into executable objects, derive
job artifact dependencies, activate root stages, and optionally reduce linear
paths into chain jobs.

## Participants

Execution scheduler, execution factory, calculator, coordinator filestorage,
execution/job/source domain types, graph, chain reducer, and worker pool lookup
callbacks created for future artifact resolution.

## Trigger

The execution scheduler claims a `new` or stale `scheduled` row and calls
`ExecutionFactory.Create` inside its scheduling transaction.

## Preconditions

Referenced source definitions must already be registered by source loading.
Artifact inputs must reference jobs already created in iteration order. Job and
source types must match the concrete definitions expected by their input type.
Downloaded external buckets/files must be reachable.

## Current behavior

1. Category statistics are loaded again; submission estimates are not persisted
   per job.
2. Inline sources are registered as definitions. External bucket definitions
   are downloaded to coordinator filestorage with a 30-minute TTL; file sources
   download one file.
3. Stages are iterated in request order and their jobs are created in list
   order. Job IDs are SHA-1 hex of execution UUID plus job name; source IDs use
   execution UUID, source name, and sometimes file name.
4. Input definitions become inline/file source IDs or artifact source IDs equal
   to the producing job ID. An artifact reference can resolve only a job already
   placed in `JobByName`.
5. Each stage's job path is reduced: while a job has exactly one still-alive
   successor in that same stage, both are combined into a `chain`. A chain sums
   time, takes maximum memory, uses the last job's ID/status/output, and removes
   internal artifact inputs from its external input set.
6. `newGraph` builds stage successors, artifact-derived job successors,
   dependency counters, cancellation flags, per-stage queues, and active root
   stages. Jobs without artifact dependencies enter their stage's `toPick`.
7. `PickJobs` drains ready jobs only from active stages. On successful job
   status, successors count a completed dependency. On any other status,
   successors are marked canceled and cancellation propagates when picked.
8. A stage finishes when its done count equals total jobs. Success activates
   dependent stages; a failed stage does not.

There is no validation or topological sort. A stage cycle or missing dependency
can yield zero active stages, which `graph.isDone` interprets as done, but the
initial scheduling path does not immediately call finish. An empty active stage
never calls `checkStageFinish` and remains active forever.

## State transitions

Conceptual transitions are `definition -> materialized execution -> graph
built`; `stage: inactive -> active -> finished`; and `job: blocked -> ready ->
picked -> done/canceled`. Only execution status is a persisted enum. Stage and
job states are inferred from maps and counters.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Submitted stage/source definitions | PostgreSQL | `Executions` JSONB | Yes | PostgreSQL |
| Materialized job/source maps | execution factory | coordinator heap | No | Current execution object |
| Graph, dependency counters, cancellation | domain graph | coordinator heap | No | Graph maps under mutex |
| Ready jobs | graph then scheduler wrapper | coordinator heap | No | `toPick` / FIFO |
| Coordinator source cache | filestorage | local filesystem | Deployment-dependent; no production volume | Local bucket metadata/files |
| Job output metadata | execution object | coordinator heap | No | `OutputByJob` |

## Persistence and transaction boundaries

Materialization runs while the selected execution row is locked `FOR UPDATE
SKIP LOCKED`. Source downloads and local filesystem writes occur inside that
database transaction but cannot be rolled back with PostgreSQL. The graph and
maps are never saved. A restart can only rebuild from the original definition;
it cannot restore dependency counters, cancellations, or completed jobs.

## Idempotency and duplicate handling

Deterministic job/source IDs make repeated materialization name-stable for the
same execution. Filestorage download treats an existing bucket/file as reusable
and can extend TTL. Rebuilding still creates a fresh graph with zero progress;
it does not deduplicate prior execution, results, messages, or artifacts.

## Concurrency and races

The graph mutex serializes `pickJobs`, `doneJob`, and `isDone`. Execution-level
maps are built before publication and then mostly read. Multiple coordinator
instances, or stale retry on the same instance, can independently build the same
definition and run it concurrently; their graph locks and heaps are unrelated.
Cross-stage duplicate names produce identical IDs and overwritten maps.

## Failure handling

A category load, source download, missing reference, unknown type, or ID error
aborts scheduling. PostgreSQL rolls back, but downloaded files can remain. The
scheduler reverses the weight increment when it sees an error, but an execution
already inserted into its in-memory map is not removed on this path. Some
type-incompatible references use unchecked `As...` assertions and may panic.

## Emitted messages/events

| Output | Condition | Durable | Notes |
| --- | --- | --- | --- |
| None during factory work | Always | N/A | `start` follows successful materialization |
| Error log | Factory/scheduling error | Log only | Definition remains eligible after rollback |
| Filestorage HTTP traffic | External source | Filesystem side effect | Not represented in history |

## Observability

Factory errors include source/stage/job names. No graph structure, root count,
cycle, chain membership, or dependency counter is exposed as a metric/event.
Scheduler job events later expose `job_type = chain` but not inner IDs.

## Implementation references

- `Exesh/internal/factory/execution_factory.go`
- `Exesh/internal/domain/execution/{execution.go,graph.go,reduce_chain_jobs.go}`
- `Exesh/internal/domain/execution/job/jobs/chain_job.go`
- `Exesh/internal/domain/execution/input` and `source`
- `Exesh/internal/provider/adapter/filestorage_adapter.go`

## Current guarantees

For a compatible, acyclic, ordered definition, IDs are deterministic within the
execution; artifact inputs create job dependencies; a job is exposed only after
all counted dependencies; and dependent stages activate only after every job in
their predecessors completed with its configured success status.

## Open questions

Should job names be unique execution-wide? Are forward artifact references and
cross-stage references supported? Is chain reduction intended to cross runtime
isolation classes? What is the required outcome for empty/cyclic graphs? See
[Open questions](open-questions.md).

## Proposed requirements

- Validate and topologically order the entire graph before any source download.
- Reject duplicate job IDs/names and empty or unreachable stages.
- Make chain eligibility require a compatible isolation/runtime policy.
- Persist or explicitly declare loss of job progress, and define replay safety.

## Test coverage

- **Existing tests / covered scenarios:** no Exesh tests; filestorage tests cover
  only its own download behavior.
- **Missing scenarios:** graph construction, cycles, missing dependencies,
  cancellation, activation, IDs, chains, and duplicate names.
- **Required integration tests:** materialize realistic Taski/Duely definitions
  with real coordinator filestorage and verify executable DAGs.
- **Required failure-injection tests:** source download/type mismatch midway
  through a claim and rollback with filesystem side effects.
