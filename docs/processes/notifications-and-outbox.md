# Notifications and transactional outbox

## 1. Purpose

Record required external side effects with database state, retry failed Taski or
Exesh calls, and attempt user notifications through WebSocket without holding
business transactions open during network I/O.

## 2. Participants

- Business handlers that insert `OutboxMessage`.
- `OutboxJob`, `OutboxDispatcher`, and the three typed handlers.
- Taski, Exesh, `WebSocketMessageSender`, and process-local connection manager.
- PostgreSQL row locking through `ForUpdateInterceptor`.

## 3. Triggers

- Any business handler records `SendMessage`, `TestSolution`, or `RunUserCode`.
- `OutboxJob` polls at 250 ms with batch 200 in production.
- Rows in `ToDo`, due `ToRetry`, or stale/due `InProgress` are claimable.

## 4. Preconditions

- Payload runtime type must match `OutboxType` for dispatcher casts.
- `RetryUntil` is supplied by every producer.
- PostgreSQL is expected for the `FOR UPDATE` interceptor semantics.
- A user notification does not require an existing/open socket to be considered successful.

## 5. Current behavior

Each job cycle first deletes rows whose `RetryUntil <= now`. It then opens a
transaction, selects an ordered batch with `FOR UPDATE`, sets `InProgress` and a
future `RetryAt`, saves, and commits the claim. Processing occurs afterward in a
new transaction. Failed handlers increment `Retries`, set `ToRetry`, and compute
exponential delay capped by `MaxRetryDelayMs`; successful handlers delete rows.
An exception rolls back the whole processing batch, including successful-row deletions.

`TestSolutionHandler` and `RunCodeOutboxHandler` propagate gateway failures.
`SendMessageOutboxHandler` always returns success after awaiting the sender.
The sender itself returns normally when no socket exists or it is not open, and
catches/logs send exceptions. Therefore those user messages are deleted rather
than retried.

## 6. State transitions

- `No outbox row -> ToDo` in a business transaction.
- `ToDo/ToRetry/due InProgress -> InProgress(RetryAt)` on claim.
- `InProgress -> deleted` on handler success.
- `InProgress -> ToRetry(Retries + 1, RetryAt)` on returned failure.
- `Any status with RetryUntil <= now -> deleted` before dispatch.

Outbox has no Done/dead-letter/audit state.

## 7. Conflicting state cleanup

Outbox processing does not change pending-duel state except through external
effects; it only deletes/updates outbox rows. Expiration does not compensate the
owning `Submission`, `CodeRun`, invitation, or duel. Business cleanup that creates
messages remains owned by its process document.

## 8. Emitted messages

### Message registry

| Type | Source/condition | Recipient | Purpose | Retry window | Process |
| --- | --- | --- | --- | --- | --- |
| `DuelStarted` | Pair converted; second creation save | Both participants | Open active duel | Duel deadline + 5 min | All duel types |
| `DuelFinished` | Finish transaction | Both participants | Fetch result | Duel deadline + 5 min | [Duel lifecycle](duel-lifecycle.md) |
| `DuelChanged` | Accepted submission | Both participants | Refresh tasks/score | Duel deadline | [Submission](submission-and-testing.md) |
| `OpponentSolutionUpdated` | Visible live solution update | Opponent | Update live code | Now + 5 min | [Duel lifecycle](duel-lifecycle.md) |
| `DuelInvitation` | New Friendly invitation | Invitee | Show invitation | Now + 5 min | [Friendly](friendly-duel-invitations.md) |
| `DuelInvitationCanceled` | Friendly sender cancel/replacement, Ranked start, shared outgoing cleanup | Sender and invitee | Close both invitation views | Now + 5 min | Friendly/Ranked/connection |
| `DuelInvitationCanceled` | Accept Friendly/Group/Tournament removes acceptor's separate outgoing invitation | Acceptor only | Close acceptor's outgoing view | Now + 5 min | Friendly/Group/Tournament |
| `DuelInvitationDenied` | Friendly deny | Original sender | Report refusal | Now + 5 min | Friendly |
| `SubmissionStatusUpdated` | First handling of submission event while not Done | Submitter | Update progress/verdict | 10 s nonterminal; 5 min with verdict | Submission |
| `CodeRunStatusUpdated` | First handling of code-run event while not Done | Owner | Update status/error | Now + 10 s | Code run |
| `GroupInvitation` | New group-membership invitation | Invited user | Show group invitation | Now + 5 min | Group membership |
| `GroupInvitationCanceled` | Inviter cancels pending membership | Invited user | Close group invitation | Now + 5 min | Group membership |
| `GroupDuelInvitation` | New Group pending duel | Each participant | Request acceptance | Now + 5 min | [Group duel](group-duels.md) |
| `GroupDuelInvitationCanceled` | Authorized Group pending deletion | Each participant | Close invitation | Now + 5 min | Group duel |
| `TournamentDuelInvitation` | Tournament sync schedules pair | Each participant | Request acceptance | Now + 5 min | [Tournament](tournament-duels.md) |

Every client message above is a `SendMessagePayload` and is attempted at most
until its row is claimed as successful/expired. Offline and send-error cases are
reported as success, so they do not use retry. A crash after an actual send but
before row deletion commits can cause a duplicate attempt.

### Non-client outbox instructions

| Type | Source | External recipient | Success effect | Duplicate risk |
| --- | --- | --- | --- | --- |
| `TestSolution` | Submission creation | Taski `/test` | Row deleted; statuses arrive later | External test can be requested again |
| `RunUserCode` | CodeRun creation | Exesh `/execute` | Returned execution id saved; row deleted | External execution can be created again |

## 9. Idempotency

Outbox itself provides durable retry, not exactly-once delivery. Row deletion is
the completion marker. An external call/send can complete before deletion commits,
so a later claim may repeat it. No message id is included in WebSocket payloads,
and Exesh dispatch has no Duely idempotency key. Taski receives submission id as
solution id, but downstream deduplication is not guaranteed here.

## 10. Transaction boundaries

- Producer atomicity is process-specific: most state/message pairs share one
  save; submission/code-run and finish use explicit transactions; duel start uses
  a later save than active-duel creation.
- Expiration, claim, and processing are three separate transactions.
- External handler calls happen inside the processing transaction but cannot be
  rolled back with PostgreSQL.
- `RunCodeOutboxHandler` saves `ExecutionId` using the same scoped context while
  the processing transaction is active.

## 11. Concurrency and race conditions

- `FOR UPDATE` serializes current claims, but the claim transaction commits before
  dispatch. If processing lasts beyond `RetryAt`, another instance can reclaim
  the same `InProgress` row.
- A process crash after external success but before delete commit repeats the side effect.
- One handler exception rolls back all row changes in that processing batch,
  even for earlier successful external calls.
- With multiple Duely instances, a notification can be claimed where the user's
  process-local socket is absent and be deleted.
- Expiration deletion can race with a long-running claimed handler.

## 12. Failure handling

- Claim/delete exceptions are logged and rolled back; the loop continues later.
- Returned gateway failure schedules retry with exponential backoff.
- Expired rows are silently deleted; no dead-letter record or business failure is written.
- Unknown `OutboxType` in dispatcher returns success, causing deletion.
- Payload/type mismatch can throw; the batch transaction rolls back.
- WebSocket absence/non-open/send exception is logged at most for exception and
  treated as successful handling.

## 13. User-visible result

Notifications are hints, not a durable event stream. Clients must recover
through HTTP and tolerate duplicates. There is no server replay after reconnect
and no acknowledgment. A state transition can commit even if its notification
never arrives; conversely a notification can arrive more than once.

## 14. Implementation references

- [OutboxMessage.cs](../../Duely/src/Duely.Domain.Models/Outbox/OutboxMessage.cs)
- [Message.cs](../../Duely/src/Duely.Domain.Models/Messages/Message.cs)
- [OutboxJob.cs](../../Duely/src/Duely.Application.BackgroundJobs/OutboxJob.cs)
- [OutboxDispatcher.cs](../../Duely/src/Duely.Application.Services/Outbox/Relay/OutboxDispatcher.cs)
- [SendMessageOutboxHandler.cs](../../Duely/src/Duely.Application.Services/Outbox/Handlers/SendMessageOutboxHandler.cs)
- [TestSolutionHandler.cs](../../Duely/src/Duely.Application.Services/Outbox/Handlers/TestSolutionHandler.cs)
- [RunCodeOutboxHandler.cs](../../Duely/src/Duely.Application.Services/Outbox/Handlers/RunCodeOutboxHandler.cs)
- [WebSocketMessageSender.cs](../../Duely/src/Duely.Infrastructure.Api.Http/Services/WebSockets/WebSocketMessageSender.cs)
- [OutboxDispatcherTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/OutboxDispatcherTests.cs)
- [SendMessageOutboxHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/SendMessageOutboxHandlerTests.cs)
- [TestSolutionOutboxHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/TestSolutionOutboxHandlerTests.cs)
- [RunCodeOutboxHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/RunCodeOutboxHandlerTests.cs)

Dispatcher/handler routing is tested. `OutboxJob` claim, expiration, retry,
multi-row rollback, and WebSocket absence/failure semantics have no tests.

## 15. Open questions

- Is best-effort, non-replayed WebSocket delivery the intended requirement?
- Should offline/send failure retain a notification or rely solely on HTTP recovery?
- What downstream idempotency guarantees do Taski and Exesh provide?
- Is a dead-letter/business failure state required after expiration?
- Should processing be outside the database transaction with a different claim protocol?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Give external instructions stable idempotency keys and specify receiver deduplication.
- Define durable notification/replay or explicitly require HTTP reconciliation.
- Distinguish no-connection/send failure from successful delivery.
- Add dead-letter/metrics and compensate queued business rows on expiration.
- Test claim/retry/crash behavior against PostgreSQL and under multiple instances.
