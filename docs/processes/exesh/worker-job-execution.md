# Worker job execution

## Purpose

Move a dispatched job through the worker queue, prepare inputs, execute a
compiler/program/checker under the selected runtime, save an optional output,
and retain a result for heartbeat delivery.

## Participants

Worker queue and slot goroutines, executor factory, compile/run/check
executors, source/output providers, local or isolate runtime, worker
filestorage, and coordinator via the next heartbeat.

## Trigger

An OK heartbeat response supplies jobs. Each worker goroutine polls the local
queue every 10ms and takes one job when available.

## Preconditions

All job source IDs must be registered in the worker source provider and their
files readable. The executor type and runtime must be supported. `isolate` and
its privileged-container prerequisites must exist for untrusted run/check jobs.

## Current behavior

1. The heartbeat loop saves returned sources, logs any save error, then enqueues
   all jobs and adds their expected memory to queued-memory accounting.
2. A slot goroutine dequeues a job, decreases free slots and available memory by
   expected memory, creates the matching executor, and always defers `Stop`.
3. `Init` creates a runtime. `PrepareInput` locates cached sources with
   filestorage read locks and copies them to fixed paths in the runtime. Each
   source lock is released before the next source is located, including when
   several inputs share one bucket.
4. Compile C++/Go uses `local.Runtime`, runs `g++` or `go build` in a
   temporary worker-container directory, and returns OK/CE or internal error.
5. C++/Go/Python run and C++ check use `isolate.Runtime`. It limits
   one process, time/wall-time, memory, per-file size, quota, file count, and
   total bytes. Run maps timeout/memory/other failures to TL/ML/RE. The checker
   interprets stderr beginning `ok` or `wrong` as OK/WA.
6. When a result claims output, `SaveOutput` reserves a bucket named by job ID,
   copies the runtime file, commits it with artifact TTL, and places its trash
   time in the result. `Worker.executeJob` only logs `SaveOutput` errors and
   returns the unchanged output-bearing result.
7. Each dispatched job creates its own runtime. Compile C++/Go executors use
   local runtime; run and check executors use isolate. Downstream jobs receive
   earlier outputs through worker artifacts.
8. The worker appends the result, frees predicted resources, and the heartbeat
   loop later sends it.

The worker config field `runtime: isolate` is loaded but not used in runtime
wiring; type factories determine runtime selection.

## State transitions

Conceptual job transitions: `received -> queued -> running -> result pending ->
reported`. Executor: `created -> initialized -> input prepared -> command done
-> output committed/failed -> stopped`. These states are not persisted.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Local job queue and resource counters | worker | Heap | No | Worker mutex/queue |
| Runtime directory/isolate box | runtime | Worker filesystem/process | No | Runtime object/files |
| Runtime paths | executor/runtime | Fixed paths in job executor and runtime directory | No | Runtime files |
| Cached sources | source provider/filestorage | Heap map + local files | Files maybe; map no | Both required |
| Output artifact and expiry | output provider/filestorage | Local files/meta | Deployment-dependent | Filestorage bucket |
| Completed result pending heartbeat | worker | Heap slice | No | `doneJobs` |

## Persistence and transaction boundaries

Worker execution does not use PostgreSQL. Filestorage reserve/commit/abort is a
filesystem transaction protected by its locks, not a database transaction.
Runtime output and result mutation are not atomic: an output can be committed
before the worker dies without a result reaching the coordinator, or a result
can claim output after save failure. Coordinator history is created only on a
later heartbeat.

## Idempotency and duplicate handling

Workers do not deduplicate job IDs in their queue or result slice. Output bucket
IDs are deterministic. Existing output files are treated as reusable by
executor save logic, which can make repeated execution appear successful and
reuse an earlier artifact/trash time. Commands themselves and displayed output
are not idempotent by contract.

## Concurrency and races

One goroutine per slot shares the queue/counters under a mutex. Separate jobs
use separate runtimes and deterministic output buckets; duplicate assignment of
the same job can race on the same bucket, with filestorage locks deciding the
winner. Runtime box IDs are mutex-allocated. Source provider maps are locked.
Shutdown cancels context only as `main` exits and does not wait for goroutines or
ensure isolate cleanup/result flush.

## Failure handling

Executor creation/init/input errors become typed internal-error results.
Runtime limit errors become domain statuses where implemented. Compile assumes
runtime usage is non-null on command error; an unexpected nil can panic.
Output-save failure is logged but not converted to a result error or
`HasOutput=false`; the coordinator can later dereference a nil trash timestamp.
`Stop` errors are logged and do not change results.

## Emitted messages/events

| Output | Condition | Durable | Notes |
| --- | --- | --- | --- |
| Typed result (`compile/run/check`) | Command path completes | Worker heap only | Sent next heartbeat |
| Artifact file/meta | Output-bearing result save | Worker filesystem | TTL-bound |
| Worker logs | Execute/save/stop | Log dependent | No custom result metric |
| Business message | Not on worker | N/A | Coordinator creates after result |

## Observability

Worker logs job IDs, command outcomes, and errors. Only default Go/process
metrics are exposed; elapsed time and used memory feed category histograms
only after coordinator recognition. There is no runtime/isolate,
queue, verdict, output-save, or sandbox violation metric.

## Implementation references

- `Exesh/internal/worker/worker.go`
- `Exesh/internal/executor` and `executor/executors`
- `Exesh/internal/runtime/{local,isolate}`
- `Exesh/internal/provider/{source_provider.go,output_provider.go}`
- `Exesh/cmd/worker/main.go`
- `Exesh/Dockerfile`, `docker-compose.yml`, and isolate submodule/config

## Current guarantees

Run/check types are wired to isolate, and the isolate runtime applies the
limits listed above. Each worker goroutine executes one dequeued job at a time
and defers runtime stop. Result/artifact durability remains separate.

## Open questions

Are compilers trusted enough for local runtime? What should happen when output
save fails? Are output contents bounded before `show_output` reads the whole
file? See
[Open questions](open-questions.md).

## Proposed requirements

- Treat output commit and artifact metadata failure as a result error.
- Add queue/job deduplication by attempt and bounded graceful shutdown.
- Define output size/display limits and runtime status mapping.
- Add security regression tests proving user binaries remain isolated.

## Test coverage

- **Existing tests / covered scenarios:** graph tests prove separate compile/run
  dispatch and rejection of removed wire types; the Taski-Exesh scenario runs a
  C++ solution in the privileged worker image and checks that the worker config
  is invisible to submitted code.
- **Missing scenarios:** executor lifecycle, verdicts, runtime selection,
  counters, duplicate jobs, output failure, shutdown, and sandbox edge cases.
- **Required integration tests:** more compile/run/check workflows in the
  privileged worker image, asserting sandbox limits and artifacts.
- **Required failure-injection tests:** runtime init/copy/run/stop failure,
  timeout/OOM/quota, output commit failure, cancellation, and process kill.
