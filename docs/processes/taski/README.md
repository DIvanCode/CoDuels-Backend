# Taski task and testing processes

This directory documents behavior observed in the current Taski, filestorage,
Exesh, and Duely code. It separates implementation facts from desired
requirements. Production currently uses REST in both event directions:
Taski polls Exesh execution history, and Duely polls Taski testing-message
history. Taski's Kafka consumer/dispatcher paths remain available but are not
the active production path.

## Process map

| Process | Document | Primary owner |
| --- | --- | --- |
| Polygon package import | [Task upload](task-upload.md) | uploader CLI and Polygon importer |
| Bucket-backed task read APIs | [Task storage and catalog](task-storage-and-catalog.md) | task storage and task use cases |
| Create an Exesh execution and a Solution | [Testing submission](testing-submission.md) | testing use case |
| Build graphs and calculate outcomes | [Testing strategies](testing-strategies.md) | strategy factory and strategies |
| Stages, jobs, sources, inputs, and statuses | [Taski to Exesh contract](exesh-execution-contract.md) | strategy serializers and Exesh API |
| Apply one Exesh event | [Execution event processing](execution-event-processing.md) | update use case |
| Consume Exesh Kafka events | [Kafka event consumption](kafka-event-consumption.md) | Kafka consumer |
| Poll Exesh message history | [REST event polling](rest-event-polling.md) | REST consumer |
| Persisted lifecycle and verdict rules | [Solution state and verdict](solution-state-and-verdict.md) | Solution and strategy |
| Public status contract | [Testing messages](testing-messages.md) | Taski message types and Duely |
| History, outbox, and Kafka delivery | [Message history and outbox](message-history-and-outbox.md) | dispatcher and PostgreSQL |
| Authoritative state by subsystem | [State ownership and persistence](state-ownership-and-persistence.md) | filestorage, PostgreSQL, Exesh |
| Restarts and broken partial flows | [Failure and recovery](failure-and-recovery.md) | all participants |

Supporting material: [open questions](open-questions.md) records ambiguities and
likely defects; the [glossary](glossary.md) distinguishes identifiers, histories,
and cursors.

## End-to-end production path

```mermaid
flowchart LR
    duely["Duely submission"] -->|POST /test| taski["Taski"]
    taski --> bucket["filestorage task bucket"]
    taski --> graph["testing strategy / Exesh graph"]
    graph --> exesh["Exesh execution"]
    exesh --> exhist["Exesh execution history"]
    exhist -->|REST polling| taski
    taski --> pg["Solution + Taski Messages in PostgreSQL"]
    pg -->|REST polling| duely
```

The Exesh history cursor is `Solution.HandledEventsCount`. The independent
Taski history cursor is Duely's `Submission.HandledStatusCount`. Neither is an
execution ID or a Taski message ID stored in the other service.

## Compatibility boundary

Task bucket JSON, task type strings, language identifiers, stage/job/source
names, serialized strategies, Exesh event shapes, and Taski public messages are
persisted or cross-service contracts. Change them only with backward-
compatibility and unfinished-Solution analysis.

## Evidence and test limitation

Taski currently contains no `*_test.go` files. The documents therefore derive
current behavior from production code, storage DDL embedded in storage types,
configuration, and the corresponding Exesh/Duely consumers. None of the
failure, concurrency, contract, or restart guarantees described as desirable
are presently demonstrated by Taski automated tests.

