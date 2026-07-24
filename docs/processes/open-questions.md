# Open questions and risks

This registry separates current observed implementation from product decisions.

## Pending state and duel creation

| Question / risk | Observed behavior | Possible consequence | Affected files | Options to decide |
| --- | --- | --- | --- | --- |
| Ranked repeat skips Friendly cleanup | Start stages outgoing Friendly deletion/messages, then returns before save when Ranked already exists. | Stale invitation and no cancellation despite success. | `StartDuelSearch.cs` | Move repeat check before cleanup, save cleanup before return, or explicitly preserve Friendly. |
| Duplicate pending rows | Pending mappings have no uniqueness constraints; handlers use check-then-insert and often `SingleOrDefault`. | Concurrent requests create duplicates; later handlers throw. | Pending EF configurations and all create/start handlers | Define per-type unique keys and handle conflicts. |
| More than one active duel per user | Maker locks both users and re-checks active state, but no database invariant represents the rule. | A future write path that omits the lock convention could create a second active duel. | `TryCreateDuel.cs`, `DuelsConfiguration.cs` | Add an active-user database invariant or keep the single-maker convention explicit. |
| Disconnect/cancel versus maker | Maker locks and reloads selected pending rows. Whichever transaction obtains the row lock first determines whether creation or cleanup wins. | A duel can still start when the maker wins immediately before disconnect cleanup. | `TryCreateDuel.cs`, `CancelPendingDuels.cs` | Decide whether disconnect must take precedence even after creation has begun. |
| Asymmetric Friendly cleanup on acceptance | Friendly/Group/Tournament acceptance deletes caller's outgoing Friendly row but notifies only caller. | Former invitee retains stale UI. | Three accept handlers | Notify both users or explicitly document single-recipient UX. |
| Same Friendly opponent ignores configuration | Existing outgoing row to same nickname returns success before comparing requested configuration. | Caller believes configuration changed when it did not; Ranked deletion staged earlier is not saved. | `CreateDuelInvitation.cs` | Include configuration in identity or expose update/replace semantics. |
| Group participant can equal itself | Group create does not reject equal user ids; acceptance always takes User1 branch. | Two messages to one user and a row that cannot reach both accepted flags. | `CreateGroupDuelInvitation.cs`, `AcceptGroupDuelInvitation.cs` | Require distinct participants and add validator/test. |
| Group membership not rechecked on accept/start | Membership is verified only at creation. | Removed member can accept and play a group duel. | Group accept/maker | Revalidate at acceptance or transactional creation. |

## Tournament behavior

| Question / risk | Observed behavior | Possible consequence | Affected files | Options to decide |
| --- | --- | --- | --- | --- |
| Concurrent tournament synchronization | Snapshot duplicate check has no lock/unique key. | Duplicate pending matches and invitations. | `SyncActiveTournaments.cs` | Transactional candidate identity/claim and DB uniqueness. |
| Draw in single elimination | Strategy advances only a non-null winner. | Tournament can remain InProgress forever after a draw. | `SingleEliminationBracketMatchmakingStrategy.cs` | Tie-break/rematch/explicit draw advancement rule. |
| Silent progress/reset | No Tournament cancel/reset/progress/finish message exists. | Client state remains stale until polling/refetch. | Tournament handlers/messages | Add durable events or explicitly require polling. |

## WebSocket and notifications

| Question / risk | Observed behavior | Possible consequence | Affected files | Options to decide |
| --- | --- | --- | --- | --- |
| Old socket removes new socket | Connection removal is by user id, not socket identity; old `finally` always cleans up. | New tab loses registration and its pending state is canceled. | `UserWebSocketHandler.cs`, `WebSocketConnectionManager.cs` | Generation-aware compare-and-remove; skip old cleanup when newer socket exists. |
| Current close failure skips cleanup | `CloseAsync` in `finally` is not caught before removal/command. | Map entry and pending states can survive a failed close. | `UserWebSocketHandler.cs` | Catch close errors and execute cleanup in nested `finally`. |
| Offline delivery counted as success | Sender returns normally for absent/non-open socket and swallows send errors. | Required transition messages are permanently lost. | `WebSocketMessageSender.cs`, `SendMessageOutboxHandler.cs` | Durable replay, retryable failure, or explicit HTTP-recovery contract. |
| Multi-instance socket routing | Connection map is process-local; any instance can claim outbox. | Message claimed on wrong replica is deleted. | Connection manager, outbox job, deployment topology | Sticky/targeted routing, shared broker, or durable inbox. |
| Ticket consume race | Ticket lookup and clear are ordinary read/update with no lock/unique index. | Same ticket may establish multiple simultaneous connects. | `GetByTicket.cs`, `UsersConfiguration.cs` | Atomic conditional update/consume and unique index. |

## Finish, testing, and outbox

| Question / risk | Observed behavior | Possible consequence | Affected files | Options to decide |
| --- | --- | --- | --- | --- |
| Partial Accepted candidate repeats | Finish query selects any duel with an Accepted submission, but returns if not all tasks solved and deadline not reached. | Same earliest candidate can delay other solved candidates. | `CheckDuelsForFinish.cs` | Query only finishable duels or process a batch and continue past no-op candidates. |
| Running submission blocks forever | Any non-Done pre-deadline submission prevents deadline finish without timeout. | Duel never finishes, ratings/messages/tournament progression stall. | `CheckDuelsForFinish.cs` | Terminal testing timeout/technical verdict policy. |
| Concurrent finish | No row lock/version protects `InProgress -> Finished`. | Duplicate messages, conflicting anti-cheat inserts, repeated updates. | `CheckDuelsForFinish.cs` | Atomic status claim/optimistic concurrency and tests on PostgreSQL. |
| Code-run authorization gap | Create validates format/user/rate only, not duel/task/participant. | Authenticated user can create runs with arbitrary duel/task category metadata. | `CodeRuns/Create.cs` | Validate ownership, visibility, duel status, and server-side limits. |
| Submission detail access | Any authenticated nonowner can read status/language/verdict/timestamps by ids; only solution/message are redacted. | Duel results or activity may be visible outside participant/group policy. | `Submissions/Get.cs` | Require participant/group-view permission or explicitly approve public metadata. |
| Count used as external cursor | REST start id is `HandledStatusCount + 1`; concurrent/duplicate events change count, while Done repeats do not save it. | Events can be skipped or repeatedly fetched. | Update handlers and REST pollers | Persist actual event id and compare atomically. |
| External outbox side effects repeat | External success precedes outbox-row deletion commit; batch rollback re-exposes rows. | Duplicate Taski test, Exesh execution, or WebSocket message. | `OutboxJob.cs`, typed handlers | Stable idempotency keys and receiver deduplication. |
| Outbox expiry has no compensation | Expired rows are deleted without updating owning business state. | Submission/code run remains Queued forever; notification silently disappears. | `OutboxJob.cs` | Dead-letter plus terminal failure/repair workflow. |

## Anti-cheat

| Question / risk | Observed behavior | Possible consequence | Affected files | Options to decide |
| --- | --- | --- | --- | --- |
| Action upload authorization | User id must match caller, but participant/task membership is not checked. | Actions can be attached to another active duel/task. | `UserActions/Create.cs` | Validate participant and task. |
| Duplicate action events | `EventId` is required but not unique. | Retries distort feature streams and suspicion scores. | `UserActionsConfiguration.cs` | Unique event id (or scoped key) with idempotent insert. |
| Late action versus finish | Active check and insert do not lock duel; finish selects/deletes concurrently. | Actions can remain after score creation/cleanup. | Action save and finish handlers | Serialize on duel or accept/reconcile late batches. |
| Duplicate scoring / batch rollback | Null scores are not claimed; one final save follows up to 20 direct Analyzer calls. | Duplicate Analyzer work; later failure repeats earlier successful predictions. | `CheckDuelsForAnticheat.cs` | Claim rows and persist per score, or specify batch atomicity. |

## Test and evidence gaps

- No WebSocket handler/manager tests: normal close, network abort, request
  cancellation, replacement, old/new race, multiple tabs, and multi-instance behavior.
- No `OutboxJob` tests for PostgreSQL locking, reclaim timing, expiration, retries,
  crash-after-send, or whole-batch rollback.
- No concurrent tests for pending creation, tournament sync, finish, rate limits,
  status cursors, or anti-cheat scoring. Maker concurrency has SQLite coverage.
- Most application tests use EF InMemory. Matchmaking rollback and concurrent
  ticks have SQLite coverage, but PostgreSQL `FOR UPDATE` behavior is not covered locally.
- No tests for combined Ranked + outgoing Friendly repeat start.
- No tests for equal Group participants or membership removal before acceptance.
- No tests for code-run duel/task authorization because the checks do not exist.
- No tests for Analyzer failure after earlier items in the same batch succeeded.
- Test-name discrepancies: `Get_incoming_ranked_invitations_*` operates on
  Friendly rows; `AcceptGroupDuelInvitationHandlerTests.Cancels_ranked_pending_and_sends_message`
  asserts that no message exists. Assertions and production types take precedence.

## Decision order

The highest-impact decisions are: decide whether to add a database active-user
invariant; make socket replacement generation-aware; define
notification durability; and define timeouts/idempotency for external testing.
Those choices constrain most remaining cleanup and retry requirements.
