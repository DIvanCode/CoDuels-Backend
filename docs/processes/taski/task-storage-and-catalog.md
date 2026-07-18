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

The file use case canonicalizes requested paths before storage access. Absolute
paths, parent-directory traversal, embedded `..` traversal, empty paths, and
invalid escapes are rejected with HTTP 400. Requests for paths that are valid but
not part of the task's public metadata are rejected with HTTP 403 before opening
the file. The public allowlist contains the statement, visible WriteCode test
inputs/outputs, optional WriteCode source code, PredictOutput code and test
input, and FindTest code. Missing task buckets and missing allowed files are
reported as HTTP 404. Internal storage or allowlist-construction failures are
reported as HTTP 500 without exposing internal details.

The handler sets an extension-derived content type and a basename
Content-Disposition, then defers both reader close and bucket unlock after a
successful open. Metadata paths are also canonicalized while building the
allowlist, so malformed metadata fails closed instead of granting access outside
the bucket. Path permission limits arbitrary file reads for well-formed metadata,
but the public-file set remains derived from task metadata rather than an
independent manifest.

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
long-held testing lock can conflict with writes/removal. The file handler closes
the reader and unlocks the bucket after streaming. Long streams still hold read
locks until the response completes.

## Failure handling

Missing bucket, write lock, bad `task.json`, unknown type, missing file, or one
bad catalog entry becomes an API error; list/metrics do not skip damaged tasks.
Empty random is an internal error. For file requests, invalid/unsafe paths are
HTTP 400, valid-but-non-public paths are HTTP 403, missing tasks or missing
allowed files are HTTP 404, and unexpected storage or metadata errors are HTTP
500. The FindTest allowlist uses the FindTest domain type and does not panic on
normal FindTest tasks. Unlock is called on normal handler exit and adapter read
errors; the file handler also closes returned readers after streaming.

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

- **Existing unit/integration tests:** Taski has focused tests for task-file
  permission checking, path canonicalization, selected HTTP status mapping,
  hidden-file denial, missing files, content headers, reader close/unlock, and
  FindTest file access.
- **Covered scenarios:** WriteCode public files and hidden tests, PredictOutput
  public files, FindTest code access, traversal rejection, invalid task keys,
  missing allowed files, service-file denial, content headers, and the former
  FindTest panic path are automated.
- **Missing scenarios:** missing/corrupt/locked bucket, one bad task in list,
  empty/random concurrency, topics distinction, and full filestorage lock
  failure injection.
- **Required contract tests:** exact DTO/task JSON polymorphism, TaskID/bucket
  mapping, content headers, allowed paths, reader close/unlock, and filestorage
  lock ownership.
- **Required failure-injection tests:** read/close/unlock failures, concurrent
  writer/removal, corrupt bucket amid catalog, large catalog, malformed metadata,
  and panic recovery for unexpected handler failures.

## Open questions

The intended filter/pagination contract, task visibility, malformed-task
isolation, remaining metadata trust boundary, and TTL policy are unresolved.

## Proposed requirements

Specify stable catalog/filter/pagination behavior; distinguish configured and
stored topics in the API; keep paths confined; preserve deterministic reader
close and unlock; isolate corrupt buckets; return a defined empty-random result;
and observe lock and scan behavior.
