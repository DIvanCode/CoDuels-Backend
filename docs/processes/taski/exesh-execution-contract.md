# Taski to Exesh execution contract

## Purpose

Define the exact execution graph that Taski produces and the fields Exesh must
accept and later echo through job events.

## Participants

Taski strategy/factory and HTTP client, Taski task-file endpoint, Exesh
Coordinator/Workers, and filestorage/artifact transfer.

## Trigger

A testing submission serializes the strategy's execution request and POSTs it
to Exesh.

## Preconditions

Every referenced task file/source/job name exists, dependency graph is valid,
languages/runtimes are supported, and Taski's download endpoint is reachable by
Exesh workers.

## Current behavior

Taski forms named stages with named dependencies and jobs; it does not locally
perform general uniqueness, missing-dependency, cycle, or artifact-producer
validation. Exesh validates/constructs the graph and returns an `ExecutionID`
or an HTTP failure. Taski saves no Solution if Exesh rejects it.

Common sources are `task` (`filestorage_bucket`, bucket=`TaskID`, Taski download
endpoint) and `suspect solution` (inline submitted text). Common stages are
`prepare` and `check`; WriteCode adds `tests X-Y` in batches of five. Compile
jobs include `prepare checker code`, `prepare suspect code`, and for FindTest
`prepare source code`/`prepare solution code`. Run names include `run suspect
code on test N`, `run source code`, and `run solution code`; checks are
`check suspect on test N` or `[suspect] check`.

Job types are compile C++/Go, run C++/Go/Python, and C++ check. Languages in
Taski are exactly `Cpp`, `Python`, `Golang`. Regular compile uses 5000 ms,
checker compile 10000 ms, and 256 MiB; checker jobs use 2000 ms/256 MiB; task
run limits come from task metadata. Run success is `OK`; WriteCode/Predict
checks expect `OK`; FindTest's final suspect check expects `Wrong Answer`.
Run output is hidden (`ShowOutput=false`) but saved as an artifact when needed.

Artifact inputs name the producer job and are used for compiled executables and
run output. Bucket inputs reference task paths; inline inputs carry caller text.
Exesh uses names to resolve dependencies/artifacts and emits the same job names,
which Taski regexes interpret for status/verdict.

**Current guarantees.** Requests are deterministic for the same task and code
under one binary version, and Exesh HTTP rejection prevents Solution insert.
Compatibility across versions is not guaranteed because names and JSON have no
version field.

## State transitions

`Task + submission -> in-memory graph -> Exesh accepted execution` or
`in-memory graph -> Exesh rejection`. Acceptance returns the only Execution ID
that Taski then attempts to persist.

## State ownership

| State | Owner | Storage | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Task files | Taski | filestorage bucket | Yes | Taski |
| Graph request copy | Taski | serialized strategy | Yes after local commit | Taski for verdict mapping |
| Accepted graph/execution | Exesh | Exesh PostgreSQL | Yes | Exesh |
| Runtime artifacts | Exesh workers/storage | worker/artifact locations | Limited by Exesh lifecycle | Exesh |
| Job-name interpretation | Taski | code + strategy JSON | JSON yes; code deploy changes | Taski version |

## Persistence and transaction boundaries

Graph creation is in memory. Exesh persistence happens in its transaction; the
Taski HTTP call is inside a separate Taski transaction. The services cannot
commit atomically. Task bucket content persists independently; later worker
downloads are not covered by the submission-time bucket lock.

## Idempotency and duplicate handling

Posting the same graph creates a new Exesh execution; no Taski idempotency key
is sent. Names are unique by construction for expected graphs, not by a generic
validator. Duplicate job/source/stage definitions introduced by change may be
detected only by Exesh or behave incompatibly.

## Ordering assumptions

Stage dependencies are expected to impose compile-before-run/check and
batch-before-next-batch. Artifact events/inputs assume producer completion.
Taski assumes emitted job names/status strings exactly match its request and
that finish follows job events.

## Concurrency and race conditions

Exesh may begin/emit before Taski commits its Solution. Worker downloads can
race task removal/change. Independent retries create independent graphs and
artifacts even when the external solution ID is the same.

## Failure handling

Serialization, HTTP, non-200, or response-decode failure rolls back local DB.
If Exesh persisted before the response failed, the execution is orphaned.
Missing files, bad dependencies/artifacts, compile/runtime/checker failures are
detected later by Exesh and returned as events; Taski maps them by names/status.
There is no graph cancellation or version negotiation.

## Emitted messages

| Condition | Message type | Recipient/channel | Payload | Persistence | Retry |
| --- | --- | --- | --- | --- | --- |
| Submit graph | HTTP request | Exesh | stages, jobs, sources, inputs, limits | Exesh on acceptance | Caller retry creates new execution |
| Accepted | HTTP response | Taski | `ExecutionID` | Taski only after later commit | No built-in reconciliation |
| Runtime progress | Exesh event/history | Taski | execution/job/type/status/error | Exesh history; Kafka optional | Mode-dependent |

## Observability

Taski/Exesh logs and both databases expose IDs and graph state. There is no
schema/version handshake, graph fingerprint, compatibility metric, orphan
correlation, artifact availability SLI, or Taski trace propagated end-to-end.

## Implementation references

- `Taski/internal/domain/testing/{execution,job,source,input}`
- `Taski/internal/domain/testing/strategy/strategies/*.go`
- `Taski/internal/api/testing/execute/{api,client}.go`
- `Exesh/internal/api/execution`
- `Exesh/internal/domain/execution`
- [Exesh execution graph](../exesh/execution-graph.md)
- [Exesh sources and artifacts](../exesh/sources-and-artifacts.md)

## Test coverage

- **Existing unit/integration tests:** none in Taski; Exesh has no Go tests in
  the examined package run either.
- **Covered scenarios:** compilation/type checking only.
- **Missing scenarios:** every strategy/language graph, invalid/duplicate/cyclic
  graph, missing artifact/file, status/name drift, rejection, and orphan.
- **Required contract tests:** golden Taski payloads accepted by current Exesh,
  exact identifiers/names/limits/statuses, event round trip, and version skew.
- **Required failure-injection tests:** Exesh persistence plus lost response,
  task download failure/removal, worker/artifact loss, malformed response,
  rolling deployment, and duplicate submit.

## Open questions

Graph versioning, validation ownership, task-file lifetime, cancellation,
idempotency, and supported rolling-upgrade compatibility are unspecified.

## Proposed requirements

Publish and version a shared contract; validate graph/name/reference invariants
before dispatch and again in Exesh; include an idempotency/correlation key;
define task/artifact retention and cancellation; and gate releases on cross-
service golden/compatibility and failure tests.
