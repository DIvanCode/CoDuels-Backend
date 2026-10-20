# Ranked matchmaking

## 1. Purpose

Let an authenticated user enter a rating-based queue, pair compatible waiting
users, and create a rated duel. Explicit search cancellation and WebSocket
disconnect have different effects on other pending states.

## 2. Participants

- Searching users and the Frontend.
- `DuelsController`, `StartDuelSearchHandler`, `CancelDuelSearchHandler`, and
  the disconnect-only `CancelPendingDuelsHandler`.
- `DuelMakingJob`, `TryCreateDuelHandler`, and `DuelManager`.
- Taski, used synchronously to obtain the task list during duel creation.
- PostgreSQL/EF Core, the outbox job, and the users' WebSockets.

## 3. Triggers

- `POST /duels/search` starts or repeats search.
- `POST /duels/cancel` cancels only the caller's Ranked search.
- Final WebSocket disconnect invokes the broader pending cleanup after its grace period.
- `DuelMakingJob` invokes `TryCreateDuelCommand` every configured interval
  (1 second in production, 3 seconds in base settings).

## 4. Preconditions

- The caller must exist.
- A caller may enter or retain search while any duel is active. A human rated
  pair starts only when neither participant has an active `IsRated` duel.
- There is no requirement for an active WebSocket, and no durable online state.
- The database has no uniqueness constraint limiting a user to one Ranked row.

## 5. Current behavior

### Start and repeat

The handler loads the user, returns success if a Ranked row already exists, or
creates one with the current rating and UTC timestamp. Existing duels and all
other pending invitations remain unchanged. Start emits no message.

The bot search job adds a missing Ranked row for each bot even when that bot
already has one or more active duels; an existing Ranked row is left intact.

### Pair selection

1. The making job loads every pending type and passes them together to
   `DuelManager`.
2. Accepted Friendly, Group, then Tournament pairs are selected first. Only a
   human already selected for a rated configuration is excluded from Ranked
   selection in the same tick. Unrated pairs can share a participant. Bots can
   appear in multiple pairs.
3. A human waiting more than seven seconds is paired with the bot whose current
   rating is closest, if one is queued. If a late human exists but no bot is
   queued, this tick selects no Ranked pair. With no late human, candidates are sorted by
   their stored rating, then queue time, and adjacent pairs are considered within
   the waiting window (`28 * elapsed seconds` for either user). The eligible pair
   with the longest waiting participant is selected.
6. At most one Ranked pair is returned per making-job iteration.

### Conversion to a duel

`TryCreateDuelHandler` obtains tasks from Taski, creates a rated, one-task,
sequential default configuration, removes the two selected Ranked rows, creates
an `InProgress` duel, and records `DuelStarted` for both users in the same
transaction. Pairing uses
the stored queue ratings, but task level and duel initial ratings use the users'
**current** ratings at creation time.

### Cancellation and disconnect

Explicit `POST /duels/cancel` deletes only the caller's Ranked rows and emits no
message. Final WebSocket disconnect still runs the broader legacy cleanup: it
deletes Ranked and outgoing Friendly rows, resets incoming Friendly and the
caller's Group/Tournament acceptance, and emits Friendly cancellation messages. See
[User connection lifecycle](user-connection-lifecycle.md).

## 6. State transitions

- `No Ranked row -> RankedPendingDuel(Rating snapshot, CreatedAt)`.
- `Ranked row exists -> success, same durable row`.
- `Two selected Ranked rows -> deleted`.
- `No duel -> Duel(InProgress, IsRated = true)`.
- `Ranked row -> deleted` on explicit search cancellation or disconnect.

## 7. Conflicting state cleanup

| State involving searching user | Start search | Successful Ranked pairing | Explicit cancel | Disconnect cleanup |
| --- | --- | --- | --- | --- |
| Ranked | Preserve or create one | Delete selected two rows | Delete caller's rows | Delete caller's rows |
| Outgoing Friendly | Unchanged | Unchanged | Unchanged | Delete all and notify both |
| Incoming Friendly | Unchanged | Unchanged | Unchanged | Keep row; reset caller acceptance |
| Group/Tournament | Unchanged | Unchanged | Unchanged | Keep row; reset caller flag |
| Active duel | Unchanged | One rated human duel checked under user locks | Unchanged | Unchanged |

No general cleanup runs when a Ranked pair is converted to a duel. Other
pending rows for those users can survive and be paired in a later job run.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| Ranked pair becomes a duel | `DuelStarted` | Each participant | Duel id | Open/refresh active duel |

No "search started", "search canceled", or "opponent found" message exists.
`DuelStarted` commits atomically with the active duel and
pending deletion. WebSocket delivery can be lost while still being marked successful; retry
and duplicate rules are detailed in [Notifications and outbox](notifications-and-outbox.md).

## 9. Idempotency

- Sequential repeated start normally returns success and keeps one Ranked row.
- A repeat emits no Ranked-specific message.
- Concurrent starts are not idempotent: both can observe no row and insert two.
- Repeating explicit cancellation succeeds and emits no message.
- Pair creation has no request key, but the maker locks both user rows and both
  Ranked rows, then reloads readiness before insertion. A concurrent tick that
  selected the same snapshot observes consumed pending rows after waiting.

## 10. Transaction boundaries

- A new start uses one `SaveChangesAsync` for Ranked insertion.
- Explicit cancellation uses one save for Ranked deletion only.
- The already-ranked early return executes no save.
- Taski is read once per maker tick before pair processing. Pair conversion has
  one explicit transaction around both saves: selected pending deletions plus
  the new duel, followed by both `DuelStarted` rows, then commit.
- Outbox dispatch is asynchronous and outside both business transactions.

## 11. Concurrency and race conditions

- Two starts can create duplicate Ranked rows; later `SingleOrDefaultAsync`
  queries may throw instead of returning a business result.
- The maker checks active rated duels by `Duel.Configuration.IsRated` while
  holding both user locks; an unrated active duel does not block Ranked creation.
- Cancel/disconnect can race after the maker loads accepted rows. The maker can
  still use its stale in-memory acceptance, or its delete can conflict with the
  cleanup delete. There is no row lock or version column.
- If duel creation commits before cleanup reads, cleanup sees no selected Ranked
  row and cannot undo the duel.
- An accepted Friendly/Group/Tournament pair leaves the user's Ranked row. A
  rated result delays that human's queued Ranked pair until it finishes.
- Multiple making-job instances can select the same rows; stable user locks,
  pending-row locks, and transactional revalidation prevent competing creation.

## 12. Failure handling

- Missing user: not-found result; no durable changes.
- Taski catalog failure happens before pair transactions and leaves the entire
  tick pending. Task-selection failure leaves that pair pending, is logged, and
  does not stop later pairs.
- Cancellation token before save: EF/external call may throw; there is no
  compensating action.
- Failure after the first duel-creation save rolls the pair transaction back, so
  no active duel is exposed without its pending rows and `DuelStarted` outbox.
  The persistence failure stops later pairs in that tick.
- Failed WebSocket delivery does not necessarily cause an outbox retry.

## 13. User-visible result

The start endpoint returns success but no queue DTO. The Frontend must infer
search state from its request state; the backend sends no Ranked status event.
On success, both participants should eventually receive `DuelStarted` and fetch
the duel. On cancellation/disconnect, there is no Ranked cancellation event.
Friendly cancellation events described above are backend facts; exact UI state
changes are a Frontend responsibility and were not inferred here.

## 14. Implementation references

- [StartDuelSearch.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Search/StartDuelSearch.cs)
- [CancelPendingDuels.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/CancelPendingDuels.cs)
- [DuelManager.cs](../../Duely/src/Duely.Domain.Services/Duels/DuelManager.cs)
- [TryCreateDuel.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/TryCreateDuel.cs)
- [DuelMakingJob.cs](../../Duely/src/Duely.Application.BackgroundJobs/DuelMakingJob.cs)
- [StartDuelSearchHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/StartDuelSearchHandlerTests.cs)
- [CancelPendingDuelsHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CancelPendingDuelsHandlerTests.cs)
- [DuelManagerTests.cs](../../Duely/tests/Duely.Domain.Tests/DuelManagerTests.cs)
- [TryCreateDuelHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/TryCreateDuelHandlerTests.cs)

Tests explicitly cover rating snapshot insertion, sequential duplicate start,
preserving other pending state, rated-duel serialization, current matching,
one Ranked pair per call, selected-row removal, and
`DuelStarted` recipients.

## 15. Open questions

- Should the rating snapshot also be used for initial ratings and task level?
- Should a queued Ranked search survive WebSocket disconnect as invitations do?
- See [Open questions](open-questions.md) for race and coverage tracking.

## 16. Proposed requirements

- Define and enforce a database invariant for at most one Ranked row per user;
  consider a database active-rated-user invariant in addition to maker row locks.
- Decide whether successful pairing must atomically record `DuelStarted` and all
  group/tournament linkage before exposing the duel.
