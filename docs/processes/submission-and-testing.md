# Submission and testing

## 1. Purpose

Persist duel submissions and custom-input code runs, reliably request work from
Taski or Exesh, consume progress, update terminal state, and notify the owning
user or both duel participants when visible duel state changes.

## 2. Participants

- Duel participant/Frontend, Duely HTTP handlers, rate limiters, and PostgreSQL.
- Taski for full task testing; Exesh for custom/sample-like code runs.
- Outbox job and `TestSolutionHandler`/`RunCodeOutboxHandler`.
- Taski/Exesh REST pollers or Kafka consumers, selected independently by config.
- WebSocket outbox messages and `DuelEndWatcherJob`.

## 3. Triggers

- `POST /duels/{duelId}/submissions` and submission GET endpoints.
- `POST /code-runs` and `GET /code-runs/{id}`.
- Outbox dispatch to Taski `/test` or Exesh `/execute`.
- REST polling of Taski/Exesh message endpoints in production, or Kafka events
  when the corresponding mode is `kafka`.
- Periodic duel finish checks after status changes.

## 4. Preconditions

- Submission: rate limit not exceeded, user and duel exist, caller is a
  participant, task key exists, and task is visible under task-order rules.
- A finished duel accepts submissions and marks them as upsolving.
- Code run: user exists, rate limit not exceeded, positive ids/limits, valid
  key/language, nonempty code, and input length <= 10,000.
- Code-run creation does **not** verify duel existence, participation, task
  existence/visibility, or duel status.

## 5. Current behavior

### Submission creation and dispatch

The handler counts the user's submissions in the last minute, validates duel
access/visibility, creates `Submission(Queued)`, and records a `TestSolution`
outbox instruction in one explicit transaction. `IsUpsolving` is true only if
the duel is already persisted as `Finished`; a deadline that passed before the
watcher finishes the duel still produces a normal submission.

The outbox handler posts task id, submission id as solution id, solution, and
language to Taski. A non-success response causes retry until the message's
deadline (duel deadline + five minutes, or now + five minutes if already past).

Submission detail lookup is not restricted to a duel participant: any
authenticated caller who knows the duel/submission ids receives status,
language, verdict, and timestamps, while only the submission owner receives the
solution and diagnostic message. Submission-list lookup gives a participant
only their own rows; a nonparticipant can see all authors' rows for a Group duel
only when their current group role has `CanViewDuel` permission.

### Submission status

Production selects REST polling. It queries every non-Done submission, starts
from `HandledStatusCount + 1`, fetches ordered events, and sends each to
`UpdateSubmissionStatusHandler`. Kafka mode builds the same command but relies
on consumer offsets and does not inspect the handler result.

The handler increments `HandledStatusCount`; an already-Done submission returns
before saving that increment or emitting a message. `start`/`status` sets
Running. Error sets Done/`Technical error`; verdict sets Done and clears message;
an Accepted verdict additionally records `DuelChanged` for both participants.
A nonempty incoming message is assigned last. Every nonterminal/first-terminal
handled event records `SubmissionStatusUpdated` for the submitter.

### Custom code run and dispatch

The code-run handler creates `CodeRun(Queued)` plus a `RunUserCode` outbox row in
one explicit transaction. Exesh dispatch creates the execution DAG, then stores
the returned `ExecutionId`. REST polling starts only after that id is persisted.
Code-run detail lookup is restricted to its owner.

### Code-run status

REST/Kafka converts Exesh events to `UpdateCodeRunCommand`. Start/compile/run
events set Running. Errors, non-OK compile/run status, OK run output, or finish
set Done according to handler rules. Each first-processed event records
`CodeRunStatusUpdated` for the owner. An already-Done run returns before saving
the count and emits nothing. The push payload has status/error but no output;
the owner fetches the run DTO for output.

## 6. State transitions

- `No Submission -> Submission(Queued, HandledStatusCount = 0)`.
- `Queued -> Running -> Done`; direct `Queued -> Done` is possible.
- `Finished duel submission -> IsUpsolving = true`.
- `No CodeRun -> CodeRun(Queued, ExecutionId = null)`.
- `ExecutionId null -> external execution id` after Exesh accepts work.
- `CodeRun Queued -> Running -> Done`; direct terminal paths are possible.
- Successful outbox instruction -> outbox row deleted; failure -> `ToRetry`.

## 7. Conflicting state cleanup

Neither submission nor code-run creation cancels pending duel state. Submission
completion can cause an active duel to finish, but does not directly delete
pending rows. Finished-duel upsolving does not change winner/ratings because the
finish job selects only `InProgress` duels; an Accepted upsolve can still enqueue
`DuelChanged` with an already-past retry deadline.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| Any processed first-time submission event | `SubmissionStatusUpdated` | Submitter | Duel/submission ids, status, message, verdict | Update submission UI |
| Accepted verdict | `DuelChanged` | Both duel participants | Duel id | Refresh solved tasks/duel state |
| Any processed first-time code-run event | `CodeRunStatusUpdated` | Run owner | Run id, status, error | Fetch/update code-run state |

These client rows commit in the same save as status changes. Repeated nonterminal
events generate repeated messages. Already-Done repeats generate none. Taski and
Exesh requests are non-client outbox instructions; delivery may repeat after an
external success if the outbox transaction does not delete the row.

## 9. Idempotency

- Submission/code-run POST has no request idempotency key; repeats create new
  rows and new external instructions.
- Terminal status handling is state-idempotent after Done, but the terminal
  event's count increment is not persisted on repeated delivery.
- Repeated nonterminal status is not message-idempotent and increments the count.
- REST recovery assumes `HandledStatusCount` equals the last contiguous external
  event id; it does not store actual event ids.
- Taski solution id uses submission id and may provide downstream deduplication,
  but Duely does not assert it. Exesh receives no Duely idempotency key.

## 10. Transaction boundaries

- Submission row and `TestSolution` outbox row: two saves inside one explicit
  transaction, committed atomically.
- CodeRun row and `RunUserCode` outbox row: same pattern.
- Each status update and its client outbox rows: one save.
- Taski/Exesh calls and REST/Kafka consumption are outside those transactions.
- Exesh external execution is created before Duely saves `ExecutionId`.

## 11. Concurrency and race conditions

- Rate limits are count-then-insert and can be exceeded by concurrent requests.
- Repeated POSTs create duplicate tests/executions.
- Two pollers/consumers can update the same row, duplicate notifications, advance
  `HandledStatusCount` incorrectly, or skip REST events.
- Enabling Kafka and REST simultaneously for the same source would make count
  semantics unsafe; configuration normally selects one.
- Outbox retry after successful Taski/Exesh response can duplicate external work.
- Exesh success followed by failed `ExecutionId` save creates an orphan execution
  and retry can create another.
- Duel finish and a late submission can race around `IsUpsolving` and deadline.
- Accepted status and finish watchers have no locking; finish may see partial status sets.

## 12. Failure handling

- Validation/access/rate-limit failures create no business row.
- Taski/Exesh request failure returns an outbox failure and schedules exponential
  retry capped by configuration until `RetryUntil`.
- REST fetch failure logs and retries a later poll. A handler failure breaks that
  entity's current event loop.
- Kafka consumer ignores failed business results and uses default auto-commit
  behavior; a consumer exception terminates its internal loop after logging.
- Missing submission/run produces not found. Delivery failures do not roll back
  status already committed.
- Submission detail intentionally redacts solution/message for a nonowner but
  does not otherwise require duel access; list access applies the participant or
  Group permission rule. Code-run detail rejects a nonowner.
- Expired outbox rows are deleted without marking submission/run failed.

## 13. User-visible result

POST returns a queued DTO. Progress arrives through WebSocket but is not replayed;
GET endpoints are recovery. Accepted submission also prompts both users to
refresh duel state. Code-run push omits output, so the owner must GET the run.
If dispatch expires, the persisted row can remain Queued indefinitely because no
failure state or compensating notification is written.

## 14. Implementation references

- [Send.cs](../../Duely/src/Duely.Application.UseCases/Features/Submissions/Send.cs)
- [Update.cs (submissions)](../../Duely/src/Duely.Application.UseCases/Features/Submissions/Update.cs)
- [Create.cs (code runs)](../../Duely/src/Duely.Application.UseCases/Features/CodeRuns/Create.cs)
- [Update.cs (code runs)](../../Duely/src/Duely.Application.UseCases/Features/CodeRuns/Update.cs)
- [TaskiSubmissionStatusRestPoller.cs](../../Duely/src/Duely.Infrastructure.BackgroundJobs/TaskiSubmissionStatusRestPoller.cs)
- [ExeshSubmissionStatusRestPoller.cs](../../Duely/src/Duely.Infrastructure.BackgroundJobs/ExeshSubmissionStatusRestPoller.cs)
- [TaskiSubmissionStatusConsumer.cs](../../Duely/src/Duely.Infrastructure.MessageBus.Kafka/TaskiSubmissionStatusConsumer.cs)
- [ExeshSubmissionStatusConsumer.cs](../../Duely/src/Duely.Infrastructure.MessageBus.Kafka/ExeshSubmissionStatusConsumer.cs)
- [SendSubmissionHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/SendSubmissionHandlerTests.cs)
- [UpdateSubmissionStatusHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/UpdateSubmissionStatusHandlerTests.cs)
- [CreateCodeRunHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CreateCodeRunHandlerTests.cs)
- [UpdateCodeRunHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/UpdateCodeRunHandlerTests.cs)

Tests cover basic creation, atomic outbox presence, status transitions, unchanged
status messages, and terminal no-op. They do not cover endpoint idempotency,
poller/Kafka offsets, external duplicate work, or concurrent rate limits.

## 15. Open questions

- Should code runs require a real accessible duel/task and participant ownership?
- Should submission detail require participant/group-view permission, rather
  than only redacting solution and message?
- What terminal state should represent expired dispatch or missing external events?
- Is `HandledStatusCount` guaranteed to correspond exactly to external event ids?
- Should post-deadline/pre-finish submissions be upsolving?
- Should Accepted upsolve emit `DuelChanged` with an already-expired retry deadline?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Add request idempotency and downstream idempotency keys.
- Validate code-run duel/task access and resource-limit policy in Duely.
- Persist actual external event ids and use optimistic concurrency/ordered claims.
- Define timeout/error transitions for permanently queued/running work.
- Add integration tests for REST/Kafka recovery and duplicate delivery.
