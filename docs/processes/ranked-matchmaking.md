# Ranked matchmaking

## 1. Purpose

Let an authenticated user enter a rating-based queue, pair compatible waiting
users, and create a rated duel. This process also defines what "cancel search"
and WebSocket disconnect actually do to the user's other pending states.

## 2. Participants

- Searching users and the Frontend.
- `DuelsController`, `StartDuelSearchHandler`, and `CancelPendingDuelsHandler`.
- `DuelMakingJob`, `TryCreateDuelHandler`, and `DuelManager`.
- Taski, used synchronously to obtain the task list during duel creation.
- PostgreSQL/EF Core, the outbox job, and the users' WebSockets.

## 3. Triggers

- `POST /duels/search` starts or repeats search.
- `POST /duels/cancel` performs the shared pending cleanup; it is not limited to Ranked.
- Any termination of `/users/connect` normally invokes that same cleanup.
- `DuelMakingJob` invokes `TryCreateDuelCommand` every configured interval
  (1 second in production, 3 seconds in base settings).

## 4. Preconditions

- The caller must exist.
- The caller must not participate in a persisted `InProgress` duel at the time
  the start handler checks.
- There is no requirement for an active WebSocket, and no durable online state.
- The database has no uniqueness constraint limiting a user to one Ranked row.

## 5. Current behavior

### Start and repeat

1. The handler loads the user and rejects an active duel.
2. It looks for one outgoing `FriendlyPendingDuel` (`User1` is the caller). If
   found, it stages deletion and two `DuelInvitationCanceled` outbox rows: one
   for the former invitee and one for the caller.
3. It looks for one existing `RankedPendingDuel` for the caller. If found, it
   immediately returns success.
4. Otherwise it creates a Ranked row with `Rating = user.Rating` and the current
   UTC `CreatedAt`, then saves the Friendly cleanup, messages, and Ranked row.

The order in step 3 has an observable exception: when Ranked and outgoing
Friendly rows already coexist, the early return occurs before `SaveChangesAsync`.
The staged Friendly deletion and cancellation messages are discarded.

### Pair selection

1. The making job loads every pending type and passes them together to
   `DuelManager`.
2. Accepted Friendly pairs have first priority, accepted Group pairs second,
   accepted Tournament pairs third. Their participants are excluded from Ranked
   selection only for this one in-memory call.
3. Ranked candidates are sorted by the **stored rating snapshot**, then wait
   time. Adjacent candidates are eligible when their difference is within both
   users' windows: `50 + 5 * whole waiting seconds`.
4. The eligible adjacent pair with the smallest rating difference wins; equal
   differences prefer the pair whose less-waiting member has waited longer.
5. If no pair is eligible and the oldest candidate has waited at least 120
   seconds, the closest adjacent pair is selected without the window limit.
6. At most one Ranked pair is returned per making-job iteration.

### Conversion to a duel

`TryCreateDuelHandler` obtains tasks from Taski, creates a rated, one-task,
sequential default configuration, removes the two selected Ranked rows, creates
an `InProgress` duel, and later records `DuelStarted` for both users. Pairing uses
the stored queue ratings, but task level and duel initial ratings use the users'
**current** ratings at creation time.

### Cancellation and disconnect

The shared cleanup removes all Ranked rows for the user without a client
message. It also deletes all outgoing Friendly invitations with notifications,
resets acceptance on incoming Friendly invitations, and resets only this user's
Group and Tournament acceptance flags. See
[User connection lifecycle](user-connection-lifecycle.md).

## 6. State transitions

- `No Ranked row -> RankedPendingDuel(Rating snapshot, CreatedAt)`.
- `Ranked row exists -> success, same durable row`.
- `Two selected Ranked rows -> deleted`.
- `No duel -> Duel(InProgress, IsRated = true)`.
- `Ranked row -> deleted` on shared cancellation/disconnect.
- `Outgoing Friendly row -> deleted` on a normally saved start request.

## 7. Conflicting state cleanup

| State involving searching user | Start search | Successful Ranked pairing | Cancel/disconnect |
| --- | --- | --- | --- |
| Ranked | Preserve one or create one | Delete the two selected rows | Delete all |
| Outgoing Friendly | Delete one and notify both; not saved on the early-return case | Unchanged unless it is the selected pair, which it cannot be in the same batch | Delete all and notify both |
| Incoming Friendly | Unchanged, including accepted | Unchanged | Keep row; set `IsAccepted = false` |
| Group | Unchanged, including accepted | Unchanged | Keep row; reset only this user's flag |
| Tournament | Unchanged, including accepted | Unchanged | Keep row; reset only this user's flag |
| Active duel | Start is rejected if already visible | Not checked by pairing | Unchanged |

No general cleanup runs when a Ranked pair is converted to a duel. Other
pending rows for those users can survive and be paired in a later job run.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| Start replaces caller's outgoing Friendly invitation and reaches save | `DuelInvitationCanceled` | Former invitee | Caller nickname, configuration id | Close incoming invitation UI |
| Same condition | `DuelInvitationCanceled` | Caller | Former invitee nickname, configuration id | Close outgoing invitation UI |
| Ranked pair becomes a duel | `DuelStarted` | Each participant | Duel id | Open/refresh active duel |

No "search started", "search canceled", or "opponent found" message exists.
All rows above are outbox rows. The cancellation rows commit with the new Ranked
row in one save. `DuelStarted` commits separately from the duel's first creation
save. WebSocket delivery can be lost while still being marked successful; retry
and duplicate rules are detailed in [Notifications and outbox](notifications-and-outbox.md).

## 9. Idempotency

- Sequential repeated start normally returns success and keeps one Ranked row.
- A repeat emits no Ranked-specific message.
- Concurrent starts are not idempotent: both can observe no row and insert two.
- Repeating shared cancellation succeeds and emits no message once all outgoing
  Friendly rows are gone.
- Pair creation has no durable idempotency key. It relies on deleting pending
  rows, without a lock covering read, selection, task lookup, and insert.

## 10. Transaction boundaries

- A normal start uses one `SaveChangesAsync`: Friendly deletion, its two outbox
  rows, and Ranked insertion are atomic.
- The already-ranked early return executes no save.
- Pair conversion first saves selected pending deletions plus the new duel.
- `DuelStarted` rows are added and saved afterward. No explicit transaction
  spans these saves or the preceding Taski call.
- Outbox dispatch is asynchronous and outside both business transactions.

## 11. Concurrency and race conditions

- Two starts can create duplicate Ranked rows; later `SingleOrDefaultAsync`
  queries may throw instead of returning a business result.
- Start can pass the active-duel check while another job creates a duel.
- Cancel/disconnect can race after the maker loads accepted rows. The maker can
  still use its stale in-memory acceptance, or its delete can conflict with the
  cleanup delete. There is no row lock or version column.
- If duel creation commits before cleanup reads, cleanup sees no selected Ranked
  row and cannot undo the duel.
- An accepted Friendly/Group/Tournament pair wins priority for this iteration,
  but the user's Ranked row remains. It can create a second duel on a later run
  because `TryCreateDuelHandler` does not filter active users.
- Multiple making-job instances can select the same rows. No unique active-duel
  constraint or claim state prevents competing creation.

## 12. Failure handling

- Missing user: not-found result; no durable changes.
- Active duel observed by start: already-exists result; no durable changes.
- Taski task-list or task-selection failure: command fails; current pair remains
  pending, while pairs committed earlier in the same loop remain committed.
- Cancellation token before save: EF/external call may throw; there is no
  compensating action.
- Failure after duel creation save but before notification save leaves an active
  duel with pending rows removed and no `DuelStarted` outbox rows.
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
outgoing Friendly cancellation and both recipients, active-duel rejection,
window/fallback matching, one Ranked pair per call, selected-row removal, and
`DuelStarted` recipients.

## 15. Open questions

- Is coexistence of Ranked with incoming/accepted Friendly, Group, or Tournament
  state intentional?
- Should the rating snapshot also be used for initial ratings and task level?
- Is the early return before saving Friendly cleanup intentional?
- Should "cancel search" have its own narrow command instead of the shared
  disconnect cleanup?
- See [Open questions](open-questions.md) for race and coverage tracking.

## 16. Proposed requirements

- Define and enforce a database invariant for at most one Ranked row per user
  and at most one active duel per user.
- Claim pending pairs transactionally and re-check active/acceptance state before
  duel insertion.
- Make start's Friendly cleanup and repeat semantics explicit and tested for the
  combined Ranked+Friendly state.
- Decide whether successful pairing must atomically record `DuelStarted` and all
  group/tournament linkage before exposing the duel.
