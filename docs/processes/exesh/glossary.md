# Exesh glossary

## Artifact

An output file stored in a worker-local filestorage bucket whose ID is the
producing job ID. The coordinator remembers which live worker advertises that
artifact and sends its HTTP endpoint to a downstream worker. There is no
coordinator copy or replica.

## Category estimate

Expected time and memory computed from PostgreSQL histograms for a job's
`category_name`. With samples, Exesh uses `0.7 * median + 0.3 * max` and clamps
time to at least `100ms` and memory to at least `16MB`; without samples it uses
the configured job limits.

## Chain

A process-local job synthesized by reducing a single-successor path within one
stage. Its ID, success status, and output are those of the last inner job; time
is summed and memory is the maximum. Inner jobs share one runtime.

## Completed result

A worker result retained in the worker's `doneJobs` slice until the next
successful heartbeat response. It is not persisted on the worker.

## Definition

The submitted sources and stages plus execution ID, weight, `tries`, status,
and timestamps. It is the only durable execution representation.

## Dispatched job

A conceptual state after a coordinator returns a job in a heartbeat response.
There is no durable dispatch record or acknowledgement distinct from the HTTP
response.

## Execution retry

Selection of a `scheduled` execution whose `scheduled_at` is older than the
configured threshold, followed by rebuilding and rerunning its definition.
It is not a retry of only the failed or missing job.

## History message

An ordered row in `Messages`, numbered per execution. REST consumers poll it
using `start_id` and `count`.

## Job

An executable node. Submitted types are `compile_cpp`, `compile_go`,
`run_cpp`, `run_go`, `run_py`, and `check_cpp`; `chain` is synthesized.

## Outbox record

A Kafka-publication intent in `Outbox`. It exists only when Kafka is enabled.
It is transactionally inserted with the corresponding history message, but a
Kafka write cannot be atomic with deletion of the row.

## Promise

A process-local scheduler reservation containing a job, a predicted worker,
and predicted start time. It is advisory: a different requesting worker can
start the job when doing so does not delay existing promises.

## Ready job

A conceptual job whose stage is active and whose artifact dependencies have
completed successfully. It sits in a per-execution process-local FIFO.

## Scheduler event

Best-effort observability data asynchronously inserted into
`exesh_execution_events`, `exesh_job_events`, or `exesh_worker_events`. It is
not the message history and is retained for seven days.

## Source

An inline value or filestorage file required as job input. The coordinator
materializes source descriptors; a worker caches the bytes and keeps a
process-local source-ID-to-file map.

## Stage

A named group of jobs with stage dependencies. A stage is active after all
predecessor stages finish without a job whose result differs from that job's
configured success status.

## Started job

A process-local coordinator entry created when a job is allocated to a worker.
The entry is removed on the first recognized completed result. There is no
timeout or persisted recovery record.

## Weight

The sum of `expected_time_ms * expected_memory_mb` over submitted job
definitions. Coordinator capacity and `nowWeight` use it; worker placement uses
expected memory separately.

## Worker removal

Deletion of a worker from the coordinator's in-memory registry after a missed
heartbeat. It is detection only, not job recovery and not artifact migration.
