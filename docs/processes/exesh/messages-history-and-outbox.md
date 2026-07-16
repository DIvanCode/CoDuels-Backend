# Messages, history, outbox, and Kafka

## Purpose

Expose ordered execution progress to polling consumers and, when enabled,
publish the same payloads asynchronously to Kafka without coupling Kafka
availability to the original execution transaction.

## Participants

Execution scheduler, message factory, dispatcher, unit of work, `Messages` and
`Outbox` storages, outbox dispatcher goroutine, Kafka writer/broker, coordinator
messages HTTP API, and external consumers (currently Duely/Taski REST pollers in
production; Kafka consumers can be enabled in other configurations).

## Trigger

Execution schedule, recognized job result, execution finish, REST
`GET /executions/{id}/messages?start_id=&count=`, coordinator startup with Kafka
enabled, or the next outbox polling/retry iteration.

## Preconditions

Every message must have a known polymorphic type and execution ID. `Send` must
run with a transaction-bearing context because storages unconditionally extract
`*sql.Tx`. Kafka publication additionally requires enabled config, brokers/topic,
and optional SASL credentials.

## Current behavior

1. Message factory produces five public types: `start`, `compile`, `run`,
   `check`, and `finish`.
2. Dispatcher marshals the message and always inserts it into `Messages`.
   Insertion locks the owning execution row `FOR UPDATE`, calculates
   `MAX(message_id)+1`, and inserts one ordered per-execution record.
3. If Kafka is disabled, no outbox row is created. This is current production
   Ansible behavior; Duely and Taski poll REST history.
4. If enabled, the dispatcher inserts an `Outbox` row in the same caller
   transaction as history and associated scheduler state. The outbox contains
   payload/time/failure fields but not execution ID as a separate column.
5. The outbox loop selects the oldest row `FOR UPDATE` without `SKIP LOCKED`,
   creates a Kafka record with outbox ID as key, writes it while holding the DB
   transaction, deletes the row, and commits.
6. A Kafka write failure updates `failed_at/failed_tries` and then returns an
   error from the transaction callback. `UnitOfWork` rolls back the update, so
   row-level failure state is not actually persisted. A process-local
   consecutive-failure counter provides exponential wait (100ms capped at the
   exponent used for 6).
7. If a row had persisted retry delay, selecting the oldest not-yet-ready row
   returns no work and prevents newer rows from being considered. Thus ordering
   is global creation order with head-of-line blocking.
8. If Kafka accepts the record but outbox deletion or transaction commit fails,
   the row remains and is published again. The key can help an external consumer
   deduplicate, but Exesh provides no exactly-once guarantee.
9. REST validates UUID, `start_id >= 1`, and `count >= 1`, then reads ordered
   rows `message_id >= start_id LIMIT count` in a transaction. There is no upper
   count bound and an unknown execution simply yields an empty list.

History, outbox, and Kafka are deliberately distinct: durable history is the
polling contract; outbox is a pending publication intent; Kafka is a delivery
attempt that may duplicate. Scheduler events are separate telemetry tables.

## State transitions

History: `absent -> committed ordered row` (never marked consumed). Outbox:
`absent -> pending -> Kafka write attempted -> deleted`, with failure intended
as `pending -> failed/retry` but the current update rolls back. Kafka record:
`not sent -> accepted`, possibly multiple times. Consumer cursor is owned by the
consumer, not Exesh.

## State ownership

| State | Owner | Stored in | Survives restart | Source of truth |
| --- | --- | --- | --- | --- |
| Message payload and per-execution ID | message storage | `Messages` | Yes | PostgreSQL |
| Pending Kafka publication | outbox storage | `Outbox` | Yes | PostgreSQL |
| Consecutive failure backoff | dispatcher | Coordinator heap | No | Loop variable |
| Kafka accepted record | Kafka | Broker | Broker policy | Kafka |
| REST consumer cursor | Duely/Taski | Consumer persistence | Consumer-defined | Consumer DB |
| Scheduler events | event recorder | Separate PostgreSQL tables | Yes after async insert | Telemetry only |

## Persistence and transaction boundaries

History and optional outbox insert use the scheduler's surrounding transaction,
so they roll back with its execution/status/histogram changes. Kafka I/O and
outbox delete occur in a later transaction and cannot be atomic with the broker.
Outbox selection locks a row for the duration. REST reads use a read transaction.
Scheduler events bypass unit-of-work transactions and use `sql.DB` asynchronously.

## Idempotency and duplicate handling

There is no unique semantic key for start/job/finish messages. Replayed
executions insert new message IDs. REST consumers can process monotonically by
message ID; Duely/Taski persist cursors in their own flows, but Exesh does not
acknowledge consumption. Kafka records use outbox ID as key but producer retries
can duplicate. Multiple coordinator dispatchers serialize on row locks, yet
commit uncertainty still duplicates.

## Concurrency and races

Locking the execution row serializes message ID allocation per execution.
Outbox dispatchers across coordinator instances contend on the global oldest
row because there is no `SKIP LOCKED`. Scheduler creation of newer outbox rows
can continue. REST readers see committed rows only. Async scheduler-event order
can lag or drop and must not be joined as exact business chronology.

## Failure handling

History/outbox insert failure aborts the owning scheduler transaction and may
turn a successful job into an execution error. Kafka outage leaves outbox rows,
but failed counters/timestamps do not persist due rollback. Broker success plus
delete/commit failure duplicates. A poison oldest row blocks all later Kafka
publication. There is no dead-letter threshold, admin replay endpoint, or writer
close/drain during shutdown.

## Emitted messages/events

| Type | Required payload | Creation condition | Transport |
| --- | --- | --- | --- |
| `start` | `execution_id`, `type` | Scheduling attempt | History; optional Kafka |
| `compile` | ID, type, `job`, `status`, optional `compilation_error` | Compile inner/normal result | History; optional Kafka |
| `run` | ID, type, `job`, `status`, optional `output` | Run inner/normal result | History; optional Kafka |
| `check` | ID, type, `job`, `status` | Check inner/normal result | History; optional Kafka |
| `finish` | ID, type, optional `error` | Terminal path | History; optional Kafka |

Job `error` results do not get compile/run/check messages; they produce only an
error-bearing finish. Domain failure statuses normally still produce a finish
without `error`.

## Observability

Dispatcher logs send attempts and errors. There are no outbox depth/age,
publication latency, duplicate, dead-letter, REST lag, or consumer metrics.
Database tables can be inspected operationally. Scheduler events are not a
substitute for public message history.

## Implementation references

- `Exesh/internal/dispatcher/message_dispatcher.go`
- `Exesh/internal/storage/postgres/{message_storage.go,outbox_storage.go,unit_of_work.go}`
- `Exesh/internal/factory/message_factory.go`
- `Exesh/internal/domain/execution/message`
- `Exesh/internal/api/messages` and `internal/usecase/messages`
- `Exesh/ansible/deploy/playbook.yml`
- REST consumers: `Taski/internal/consumer/rest_consumer.go` and Duely
  `ExeshClient.cs`

## Current guarantees

On a successful owning transaction, history payload and any enabled outbox
intent commit with the associated scheduler mutation. Per-execution history IDs
are strictly increasing under the execution row lock. REST returns them ordered.
Kafka is at-least-once at best and is disabled in current production deployment.

## Open questions

Which transport is canonical long term? Must message schemas be versioned?
Should infrastructure errors emit a job message? How should poison rows and
consumer deduplication work? See [Open questions](open-questions.md).

## Proposed requirements

- Treat public message schemas/order/conditions as versioned contracts.
- Persist outbox failure state in a committed transaction and add dead-letter
  handling without global head-of-line blocking.
- Document at-least-once Kafka and require consumer dedupe by stable event ID.
- Bound REST page size and distinguish unknown executions if consumers need it.
- Add outbox/history lag, depth, age, retry, and duplicate observability.

## Test coverage

- **Existing tests / covered scenarios:** none in Exesh.
- **Missing scenarios:** schemas/order, rollback, REST pagination, outbox,
  Kafka retry/duplicates, multiple dispatchers, and Kafka-disabled operation.
- **Required integration tests:** PostgreSQL history/outbox plus broker and REST
  consumers, including two dispatcher processes.
- **Required failure-injection tests:** broker failure, success plus DB
  delete/commit failure, poison oldest row, restart, and consumer redelivery.
