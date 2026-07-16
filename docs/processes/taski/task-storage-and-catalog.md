# Task storage and catalog

## Purpose

Serve task metadata, lists, random selection, configured topics, statements,
and approved task files from committed filestorage buckets.

## Participants

HTTP caller (usually Duely/browser), Taski HTTP handlers, task use cases,
task storage adapter, filestorage, and task metrics collector.

## Trigger

Task get/list/random/topics/file HTTP request, a strategy load during testing,
or a periodic metrics collection tick.

## Preconditions

A committed bucket named by a valid 40-hex `TaskID` exists and contains a
parsable `task.json` whose `type` is `write_code`, `find_test`, or
`predict_output`.

## Current behavior

`TaskID` equals bucket ID. `Get` obtains a filestorage read lock, reads and
polymorphically unmarshals `task.json`, and returns the task plus `unlock`.
Errors after lock acquisition release it. `GetFile` obtains the same kind of
lock and opens a joined path; callers own both reader and unlock lifetimes.

List enumerates every bucket and fully reads every task; one corrupt/locked
bucket fails the complete result. Random enumerates IDs, chooses uniformly from
that in-memory slice using `math/rand/v2`, then loads the task. Empty storage is
an error (HTTP 500). Neither list nor random filters by level or task topics.
The topics endpoint returns configuration, not the union of per-task topics.
Metrics likewise scan all tasks periodically.

The file use case only permits paths referenced by the task's public metadata
(statement, visible tests, or task-type-specific public files), then the handler
sets extension-derived content type and a basename Content-Disposition. It
defers `unlock` but does not explicitly close the returned reader. Its
`FindTest` branch asserts `PredictOutputTask`, which can panic. Exact path
permission limits arbitrary file reads for well-formed metadata, but metadata
paths themselves are trusted; joining/cleaning does not establish bucket-root
containment.

**Current guarantees.** Committed buckets have read locking, type dispatch is
explicit, and bucket IDs must parse as Task IDs. There is no catalog snapshot,
filter, pagination, TTL refresh/removal, or corrupt-bucket isolation guarantee.

## State transitions

Reads do not intentionally mutate business state:
`committed unlocked bucket -> read-locked -> unlocked`. A write lock or missing
bucket/file produces an error instead of a partial task response.

## State ownership

| State | Owner | Storage | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Task metadata/files | Taski via filestorage | committed bucket | Yes | bucket and `task.json` |
| Read lock | filestorage | process/filesystem lock state | Lock semantics depend on storage | filestorage |
| Configured topics | Taski config | YAML/environment | Yes when redeployed | runtime config |
| Catalog result/random choice | use case | memory/response | No | bucket enumeration |

## Persistence and transaction boundaries

No PostgreSQL transaction participates. The bucket lock covers metadata/file
reading until the returned `unlock` is called; testing deliberately holds it
through the Exesh HTTP request. Task API handlers normally defer unlock. Buckets
are uploaded with nil TTL and are not automatically refreshed or deleted here.

## Idempotency and duplicate handling

Metadata/file GETs are read-only. Random selection is intentionally not
repeatable and has no seed/request key. Duplicate bucket IDs cannot exist in
one storage namespace. Repeated scans repeat all filesystem work.

## Ordering assumptions

No stable list ordering is promised. Randomness is over the current bucket-ID
slice. Task JSON field names/type/language strings and referenced file paths are
compatibility assumptions.

## Concurrency and race conditions

Read locks prevent incompatible bucket mutation according to filestorage lock
semantics. Concurrent catalog requests independently scan all buckets. A
long-held testing lock can conflict with writes/removal. The file reader leak
can retain OS resources even after unlock.

## Failure handling

Missing bucket, write lock, bad `task.json`, unknown type, missing file, or one
bad catalog entry becomes an API error; list/metrics do not skip damaged tasks.
Empty random is an internal error. A wrong type assertion may panic the
`FindTest` file path. Unlock is called on normal handler exit and adapter read
errors, but reader close is absent in the file handler.

## Emitted messages

| Condition | Message type | Recipient/channel | Payload | Persistence | Retry |
| --- | --- | --- | --- | --- | --- |
| Successful read | HTTP response | caller | task DTO/list/topics/file | None | Caller controlled |
| Failed read | HTTP error | caller | status/error envelope | None | Caller controlled |

## Observability

HTTP and storage errors are logged. Prometheus exposes per-task counts/solution
timing, but task labels can remain stale and a corrupt bucket aborts collection.
No lock-duration, scan-cost, reader-count, corrupt-bucket, or random-distribution
metric exists.

## Implementation references

- `Taski/internal/storage/filestorage/task_storage.go`
- `Taski/internal/usecase/task/usecase/{get,list,random,topics,file}/usecase.go`
- `Taski/internal/api/task/*`
- `Taski/internal/domain/task/tasks/*.go`
- `Taski/internal/metrics/collector.go`
- `Backend/filestorage` locking and bucket implementation

## Test coverage

- **Existing unit/integration tests:** none in Taski.
- **Covered scenarios:** none are automated.
- **Missing scenarios:** all task types, missing/corrupt/locked bucket, one bad
  task in list, empty/random concurrency, topics distinction, traversal,
  service-file denial, reader close/unlock, and `FindTest` file access.
- **Required contract tests:** exact DTO/task JSON polymorphism, TaskID/bucket
  mapping, content headers, allowed paths, and filestorage lock ownership.
- **Required failure-injection tests:** read/close/unlock failures, concurrent
  writer/removal, corrupt bucket amid catalog, large catalog, and handler panic.

## Open questions

The intended filter/pagination contract, task visibility, malformed-task
isolation, path trust boundary, TTL policy, and reader ownership are unresolved.

## Proposed requirements

Specify stable catalog/filter/pagination behavior; distinguish configured and
stored topics in the API; confine paths; close and unlock deterministically;
isolate corrupt buckets; return a defined empty-random result; and observe lock
and scan behavior.
