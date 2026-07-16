# Taski glossary

| Term | Meaning in current code |
| --- | --- |
| `TaskID` | Forty-character lowercase SHA-1 hex string; also the filestorage bucket ID. Polygon import hashes the trimmed `short-name`. |
| Task bucket | Committed filestorage directory containing `task.json`, statement, tests, solution, and checker files. |
| `ExternalSolutionID` | Caller-supplied ID, normally the decimal Duely Submission ID. Stored in Taski `Solutions.external_id`. |
| Taski Solution | PostgreSQL row linking caller ID, Task ID, source text, language, serialized strategy, and Exesh `ExecutionID`. |
| `ExecutionID` | Identifier returned by Exesh after it accepts an execution graph. It is not the Taski Solution's external ID. |
| Strategy | Persisted, task-type-specific graph plus mutable job outcomes, status, verdict, and message. |
| Exesh event | `start`, `compile`, `run`, `check`, or `finish` notification about an execution/job. |
| Exesh execution history | Exesh-owned per-execution messages polled by Taski in production. |
| `HandledEventsCount` | Taski Solution field used as the next Exesh REST history cursor; it counts accepted processed events. |
| Testing message | Taski-owned public `start`, `status`, or `finish` message addressed by `ExternalSolutionID`. |
| Taski message history | Taski `Messages` rows, with a per-external-solution `message_id`, polled by Duely in production. |
| `HandledStatusCount` | Duely Submission field used as its next Taski message-history cursor. |
| Outbox | Optional Taski PostgreSQL queue for Kafka publication of testing messages; disabled in production. |
| Bucket lock | filestorage read lock returned with a task/file and released through an `unlock` callback. |
| Artifact input | Exesh input that consumes output produced by another named job. |
| Verdict | Final Taski result such as `Accepted`, `Wrong Answer`, or `Testing Failed`. |
| Current guarantee | Property directly established by the examined implementation, not a product promise. |

