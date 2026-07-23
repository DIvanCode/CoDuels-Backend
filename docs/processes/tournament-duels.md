# Tournament duels

## 1. Purpose

Create group tournaments, progress their bracket or round-robin schedule, ask
available participants to accept each match, attach resulting duels to the
tournament, and mark the tournament finished.

## 2. Participants

- Tournament creator/authorized group members and selected group participants.
- Tournament HTTP handlers and `TournamentSynchronizationJob`.
- `SingleEliminationBracketMatchmakingStrategy` or `GroupStageMatchmakingStrategy`.
- `AcceptTournamentDuelHandler`, `DuelManager`, and `DuelMakingJob`.
- PostgreSQL, Taski, outbox delivery, and WebSockets.

## 3. Triggers

- `POST /tournaments`: create a `New` tournament.
- `POST /tournaments/{id}/start`: move `New` to `InProgress`.
- Synchronization job every configured interval (1 second in production).
- `GET /duels/tournament/invitations`: list caller's unaccepted matches.
- `POST /tournaments/{id}/duels/accept`: accept the caller's pending match.
- Shared `POST /duels/cancel` or WebSocket cleanup: reset caller acceptance.
- Duel-making and duel-finish jobs progress matches and results.

## 4. Preconditions

- Create/start require the group permission checked by `GroupPermissionsService`.
- At least two unique, nonempty nicknames are validated; each must be a
  non-pending group member at creation.
- Optional duel configuration must exist; ownership is not checked.
- Synchronization considers only `InProgress` tournaments and only candidates
  whose users are absent from all active duels and all four pending types in its snapshot.
- Accept requires an existing user, no observed active duel, and one Tournament
  row for that user and tournament. It does not take opponent/configuration.

## 5. Current behavior

### Tournament creation and start

Creation builds the selected strategy subtype, randomizes seed values, adds
participants, initializes bracket nodes or an empty group-stage duel-id list,
and saves once with status `New`. Start changes only `New -> InProgress`; calling
start for an already `InProgress` or `Finished` tournament returns success with
the unchanged DTO.

### Synchronization and pending creation

For every active tournament, the job loads referenced duels, calls strategy
`Sync`, obtains candidate pairs, skips an already represented Tournament row,
and skips any candidate whose participant is in the global busy-user snapshot.
It creates a `TournamentPendingDuel(false, false)` and one
`TournamentDuelInvitation` per participant, then marks both users busy in memory
so this invocation will not schedule them again. All tournament updates, new
pending rows, and messages are saved once after processing all tournaments.

Single elimination advances a node from a finished duel's non-null winner and
finishes when the root has a winner. A draw does not advance the node. Group
stage proposes every unplayed unordered participant pair, records attached duel
ids, and finishes when the expected `n*(n-1)/2` referenced duels all finish.

### Acceptance and duel creation

Accept deletes the caller's one Ranked row and one outgoing Friendly row; only
the caller gets a Friendly cancellation message. It then sets the caller's
positional Tournament flag. Both flags are required by `DuelManager`.
Selected Tournament rows are deleted with the active duel. A later save attaches
the duel to its bracket node or group-stage id list and records `DuelStarted` for
both users, but both saves are enclosed by one pair transaction and commit together.

### Reset and subsequent rounds

Shared cancel/disconnect keeps the Tournament row but resets only that user's
flag, without a Tournament message. Finished duels are observed by later sync
runs, which advance the structure and enqueue newly available matches.

## 6. State transitions

- `No tournament -> Tournament(New, participants, initialized strategy state)`.
- `New -> InProgress` on first start.
- `InProgress + ready candidate -> TournamentPendingDuel(false, false)`.
- `User1/User2 false -> true` on individual acceptance.
- `This user's true -> false` on shared cancel/disconnect.
- `TournamentPendingDuel(true, true) -> deleted; Duel(InProgress) created`.
- `No referenced duel -> duel id attached` before the creation transaction commits.
- `InProgress -> Finished` when the strategy's completion condition is met.

## 7. Conflicting state cleanup

| State | Synchronization | Tournament accept | Shared cancel/disconnect | Tournament duel creation |
| --- | --- | --- | --- | --- |
| Active duel | Candidate is skipped | Caller is rejected | Unchanged | Re-checked after locking both users |
| Any existing pending type | Candidate is skipped | Ranked and caller's outgoing Friendly deleted; other rows unchanged | Type-specific shared cleanup | Only selected Tournament row deleted |
| Other Tournament pair | Makes user busy for this run | Unchanged | Caller flag reset | Unchanged |

Tournament synchronization is the only creator that proactively treats all
pending types as busy. That protection is a snapshot, not a reservation, and
later HTTP handlers can create conflicting state.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| New scheduled match | `TournamentDuelInvitation` | Each participant | Tournament id/name, opponent, configuration | Request acceptance |
| Accept deletes caller's outgoing Friendly invitation | `DuelInvitationCanceled` | Caller only | Former invitee/configuration | Close separate outgoing invitation |
| Accepted match becomes duel | `DuelStarted` | Both participants | Duel id | Enter match |

There is no Tournament acceptance, reset, cancellation, round-advanced, or
tournament-finished client message. Pending creation and two invitation outbox
rows commit together. Tournament attachment and `DuelStarted` share the second
duel-creation save; the first and second saves become visible together at commit.

## 9. Idempotency

- Create has no client idempotency key; repeated requests create new tournaments.
- Start is sequentially idempotent for any non-`New` status.
- One synchronization invocation avoids an existing same-pair row; repeated
  sequential sync therefore does not duplicate it.
- Accept is convergent while the row exists, then returns not found after consumption.
- Concurrent synchronization instances can both create the same pair and messages.
- Strategy `AttachDuel` avoids a duplicate group-stage duel id; bracket attach
  writes the located node's duel id.

## 10. Transaction boundaries

- Tournament create and start each use one save.
- A synchronization pass uses one final save for strategy state, every new
  Tournament row, and all invitation outbox rows.
- Accept uses one save for acceptance plus Ranked/Friendly cleanup and message.
- Taski's catalog is fetched once per maker tick before pair transactions. Duel
  creation uses one pair transaction around pending deletion and active-duel
  insertion, followed by Tournament attachment and both `DuelStarted` rows.

## 11. Concurrency and race conditions

- Concurrent sync jobs lack row claims/unique constraints and can duplicate
  Tournament rows and invitations.
- Busy-user calculation can become stale before insertion.
- Accept/reset/maker races serialize when the maker locks and reloads the selected
  Tournament pending row; a committed reset prevents creation.
- `AcceptTournamentDuelHandler` uses `SingleOrDefault` by tournament and user;
  duplicate or multiple simultaneous matches for one user can throw.
- The maker locks both users and re-checks active-duel state after sync/accept.
- Failure after active duel insertion but before strategy attachment rolls the
  pair transaction back, leaving the Tournament pending row available for retry.
- Single-elimination draws leave a node without a winner, so the bracket cannot progress.
- Multiple finished-duel/sync instances can update serialized strategy state
  without optimistic concurrency tokens.

## 12. Failure handling

- Missing group/configuration/participant or permission failure stops creation.
- Unknown tournament/user/pending row stops start or accept as their handlers specify.
- Analyzer/Taski are not called by synchronization; a database failure rolls
  back that pass's tournament changes, rows, and invitation messages.
- Task selection failure leaves the accepted Tournament row pending.
- A failed second duel-creation save rolls back the active duel, pending deletion,
  Tournament linkage, and `DuelStarted` messages; later pairs still run.
- Delivery failure does not roll back the scheduled Tournament row.

## 13. User-visible result

Creation/start return tournament DTOs. Participants receive invitations and can
recover only their own unaccepted matches through GET. Acceptance/reset is
silent. The tournament details endpoint is the recovery source for bracket or
group-stage progression because no progress/finish messages exist. `DuelStarted`
is the only push signal that an accepted match became active.

## 14. Implementation references

- [CreateTournament.cs](../../Duely/src/Duely.Application.UseCases/Features/Tournaments/CreateTournament.cs)
- [StartTournament.cs](../../Duely/src/Duely.Application.UseCases/Features/Tournaments/StartTournament.cs)
- [SyncActiveTournaments.cs](../../Duely/src/Duely.Application.UseCases/Features/Tournaments/SyncActiveTournaments.cs)
- [AcceptTournamentDuel.cs](../../Duely/src/Duely.Application.UseCases/Features/Tournaments/AcceptTournamentDuel.cs)
- [SingleEliminationBracketMatchmakingStrategy.cs](../../Duely/src/Duely.Domain.Services/Tournaments/SingleEliminationBracketMatchmakingStrategy.cs)
- [GroupStageMatchmakingStrategy.cs](../../Duely/src/Duely.Domain.Services/Tournaments/GroupStageMatchmakingStrategy.cs)
- [TournamentSynchronizationJob.cs](../../Duely/src/Duely.Application.BackgroundJobs/TournamentSynchronizationJob.cs)
- [SyncActiveTournamentsHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/SyncActiveTournamentsHandlerTests.cs)
- [AcceptTournamentDuelHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/AcceptTournamentDuelHandlerTests.cs)
- [SingleEliminationBracketMatchmakingStrategyTests.cs](../../Duely/tests/Duely.Domain.Tests/SingleEliminationBracketMatchmakingStrategyTests.cs)
- [GroupStageMatchmakingStrategyTests.cs](../../Duely/tests/Duely.Domain.Tests/GroupStageMatchmakingStrategyTests.cs)

Tests cover sequential duplicate suppression, both acceptance positions,
Ranked/Friendly cleanup, bracket attachment, acceptance gating, and strategy
progression. Maker tests inject a failure between creation saves and verify
rollback; concurrent tournament synchronization remains uncovered.

## 15. Open questions

- What is the required outcome of a drawn single-elimination duel?
- Should start reject `Finished` instead of returning success?
- Should a tournament match remain pending indefinitely after repeated disconnects?
- Should synchronization and pending creation use a durable candidate key?
- Should progress/finish/reset have client messages?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Add a unique candidate identity and transactional scheduler claim.
- Define draw/tie-break behavior for single elimination.
- Define recovery and notification requirements for tournament progress and finish.
