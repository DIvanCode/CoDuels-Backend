# Taski open questions

These questions are not resolved by code or tests. Suspected defects are not
silently converted into requirements.

| Area | Current observation | Open question / decision needed |
| --- | --- | --- |
| Uploader trust | Missing outputs execute the Polygon main solution directly on the uploader host, with a 10-second run timeout but no isolation, memory/output cap, or compile timeout. | Are Polygon packages trusted operational input, or must output generation run through Exesh/isolate? |
| Task identity | ID is SHA-1 of only trimmed `short-name`; an existing bucket makes re-upload fail. | Is immutability intentional, and how are corrected versions and collisions represented? |
| Package paths | ZIP entry traversal is rejected, but paths read from `problem.xml` are cleaned without a package-root containment check. | Must every metadata-derived source path be confined to the package root? |
| Import fidelity | Import selects one testset, basic statement fragments, one solution/checker, and discards groups, points, validators, interactors, and other Polygon data. | Which Polygon semantics are intentionally supported? |
| Memory conversion | Bytes are divided by MiB with integer truncation; positive limits below one MiB become zero. | Does zero mean unlimited or invalid downstream? |
| Catalog | List/random have no level/topic filters; configured topics are independent of task topics. | Which filtering contract should callers rely on? |
| File API | `FindTest` permission checking type-asserts to `PredictOutputTask`; returned files are unlocked but not explicitly closed. | Are these defects, and who owns reader closure? |
| Submission idempotency | No idempotency key or unique index protects external/execution IDs. | Should one external submission map to exactly one Taski Solution and Exesh execution? |
| Cross-service atomicity | Exesh is called inside the Taski DB transaction before Solution insert/commit. | What reconciliation or compensating cancellation owns orphan executions? |
| Event-before-row race | Kafka can receive an event before the Solution commits; unknown executions are ignored and the Kafka offset is committed. | Must unknown events be retained/retried instead? |
| Cursor semantics | REST dedupe compares `message_id` to a count and increments rather than assigning the observed ID. | Is contiguous, gap-free Exesh history a permanent contract? |
| Event ordering | Finish before start produces a finished technical failure and later events are ignored. | Must Taski buffer/reorder events or is Exesh ordering contractual? |
| Unknown jobs | Strategy ignores unknown names; an initial status message can still be generated. | Should incompatible job events fail, quarantine, or be ignored? |
| Serialized strategy | JSON has no version and job/stage names plus regexes drive restored behavior. | What migration/versioning policy supports rolling upgrades and old in-progress rows? |
| Public message order | Kafka delivery and REST history are separate modes with different duplicate/order properties. | What order and dedupe guarantees does Duely require? |
| Outbox retry | Retry due-time condition is inverted, and failure updates are rolled back with the transaction error. | What retry/dead-letter policy is intended? |
| In-progress lifetime | Missing finish leaves a Solution in progress forever; there is no timeout/cancellation/reconciliation job. | What terminal policy owns lost executions? |
| Metrics | There is no event lag, orphan, cursor, outbox backlog, or stuck-Solution metric. | Which service-level indicators are required? |

