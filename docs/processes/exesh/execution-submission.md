# Execution submission

## Purpose

Accept an execution definition, estimate its scheduling weight, persist it as
`new`, and return an identifier that callers can use to poll messages.

## Participants

Caller (currently Duely or Taski), coordinator `POST /execute` handler, execute
use case, calculator, category histogram storage, unit of work, and PostgreSQL.
The schedulers and workers do not participate before the transaction commits.

## Trigger

An HTTP `POST /execute` request containing `sources` and `stages`.

## Preconditions

The JSON must decode through the polymorphic source, input, and job definition
types. PostgreSQL and all tables initialized at coordinator startup must be
available. No authentication or caller-supplied idempotency key is checked by
Exesh.

## Current behavior

1. The handler decodes JSON. Unknown polymorphic types or malformed JSON return
   HTTP 400 with `status: ERROR`.
2. The use case rejects duplicate source names and duplicate stage names. It
   rejects duplicate job names only within the same stage.
3. In one database transaction it loads category histograms, estimates every
   job, sums weight, constructs a UUID execution definition, and inserts it.
4. The inserted definition has `tries = 0`, `status = new`, `created_at = now`,
   and null scheduled/finished timestamps.
5. After commit, HTTP 200 returns `status: OK` and `execution_id`.

The submission path does not validate empty graphs, cycles, undefined stage
dependencies, cross-stage duplicate job names, limits, success statuses,
source/input type compatibility, artifact direction, or whether a referenced
job has already been materialized. Many such errors appear only when the
scheduler factory later rebuilds the definition; a mismatched concrete source
type can also panic at a type assertion.

## State transitions

`Execution: absent -> new` is durable on commit. No job, stage, promise, worker,
or artifact state is created during submission.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Definition and UUID | execute use case | `Executions` | Yes | PostgreSQL row |
| Status and tries | execution domain | `Executions` | Yes | PostgreSQL row |
| Weight and estimates used to calculate it | calculator | weight only in `Executions`; histograms in PostgreSQL | Yes | row and histogram tables |
| Graph and ready queue | execution scheduler | Not created yet | No | None at this point |
| Message history/outbox | dispatcher | Not created yet | N/A | None at this point |

The complete inventory is in
[State ownership and persistence](state-ownership-and-persistence.md).

## Persistence and transaction boundaries

Histogram reads and `INSERT INTO Executions` use the transaction placed in the
context by `UnitOfWork.Do`. A load or insert failure rolls back and no ID is
returned. There is no external call in this transaction. Table initialization
also happens at startup in a transaction, while scheduler event tables are
initialized separately through `sql.DB`.

## Idempotency and duplicate handling

Repeating an identical request creates a different UUID and a second execution;
payload equality is not checked. A network timeout after commit can therefore
lead the caller to submit again. The primary key prevents duplicate UUIDs, but
UUIDs are server-generated and are not a practical request-deduplication key.

## Concurrency and races

Submissions do not lock one another except through normal histogram/table
operations. Category histogram updates from completions can occur concurrently;
the read sees PostgreSQL's transaction snapshot. Validation is purely within
one request and provides no global semantic constraint.

## Failure handling

Decode errors are 400. Validation, histogram, transaction, or insert errors are
all surfaced as generic HTTP 500 `failed to process execute`; the detailed
reason is logged. A successfully persisted but semantically invalid definition
can remain `new` and repeatedly fail later scheduler attempts.

## Emitted messages/events

| Output | Condition | Durable | Notes |
| --- | --- | --- | --- |
| HTTP `execution_id` | Transaction committed | Response only | Means accepted, not started |
| Coordinator log `created execution` | After commit | Log retention dependent | Includes ID and weight |
| Execution history/Kafka event | Never in this process | N/A | `start` is emitted by scheduling |

## Observability

Logs distinguish invalid commands, storage failures, and committed execution
weight. There is no submission counter, validation-reason metric, trace ID, or
request idempotency field. Scheduler tables have no submission event.

## Implementation references

- `Exesh/internal/api/execute/{api.go,handler.go}`
- `Exesh/internal/usecase/execute/usecase.go`
- `Exesh/internal/calculator/calculator.go`
- `Exesh/internal/domain/execution/execution_definition.go`
- `Exesh/internal/storage/postgres/{unit_of_work.go,execution_storage.go}`
- Consumer builders: `Duely/...Gateway.Exesh/ExecutionFactory.cs` and
  `Taski/internal/api/testing/execute/client.go`

## Current guarantees

A successful response follows a committed unique execution row containing the
decoded definition and calculated weight. Source/stage names and per-stage job
names are unique according to the limited checks above. No stronger graph or
execution guarantee follows from acceptance.

## Open questions

Should clients supply an idempotency key? Which graph, resource, file-path, and
status invariants belong at the API boundary? Should invalid definitions be
rejected as 4xx with stable error codes rather than becoming scheduler poison
rows? See [Open questions](open-questions.md).

## Proposed requirements

- Define and test a complete semantic validator before persistence.
- Add caller-scoped idempotency with a durable uniqueness constraint.
- Specify positive and maximum limits, valid success statuses, graph rules, and
  permitted source/input combinations.
- Preserve the current acceptance contract only after consumers are updated.

## Test coverage

- **Existing tests / covered scenarios:** no committed Exesh tests; consumer
  tests do not establish coordinator guarantees.
- **Missing scenarios:** decoding, semantic validation, weight edges,
  concurrency, idempotency, and rollback.
- **Required integration tests:** real PostgreSQL submission and response-after-
  commit behavior.
- **Required failure-injection tests:** histogram/insert/commit failure and lost
  response followed by duplicate submission.
