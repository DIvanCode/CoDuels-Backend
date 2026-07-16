# Post-duel anti-cheat

## 1. Purpose

Collect ordered user behavior during active duels, retain only user/task action
streams that produced an accepted solution, obtain Analyzer suspicion scores,
and optionally delete raw actions after scoring.

## 2. Participants

- Frontend action batches and `UserActionsController`/`SaveUserActionsHandler`.
- `CheckDuelsForFinishHandler`, `AnticheatScore`, and PostgreSQL.
- `AnticheatBackgroundService`, `CheckDuelsForAnticheatHandler`, and Analyzer.

## 3. Triggers

- Authenticated action-batch HTTP requests during a duel.
- Duel finish transaction creates score work and cleans irrelevant streams.
- Anti-cheat background loop every 5 seconds in production.

## 4. Preconditions

- Every action's `UserId` must equal the authenticated command user.
- Actions pass structural validation and are saved only for duel ids currently
  observed as not Finished.
- The save handler does not verify that the user participates in that duel or
  that the task key exists.
- A score is created only for a distinct participant/task with a Done/Accepted submission.

## 5. Current behavior

Action batches containing any other user id fail entirely. Otherwise, actions
for active duel ids are inserted and actions for missing/finished ids are silently
filtered; an empty filtered batch returns success. `EventId` and sequence have no
unique constraint, so retransmission inserts duplicates.

During finish, accepted user/task pairs become composite-keyed
`AnticheatScore(Score = null)`. All actions whose `userId:taskKey` is not among
those pairs are deleted. This work commits atomically with duel status, ratings,
winner, and finish outbox rows.

The background handler loads at most 20 null scores ordered by duel/user/task.
For each, it loads actions ordered by `SequenceId`, selects the user's initial
duel rating, calls Analyzer, and stages the returned score. If configured
`ShouldCleanupUserActions` (true in base/production settings), that stream is
staged for deletion. One save occurs only after the whole batch succeeds.

## 6. State transitions

- `No action row -> UserAction` for eligible active duel ids.
- `Duel finishing + accepted pair -> AnticheatScore(null)`.
- `Irrelevant action stream -> deleted` in finish transaction.
- `AnticheatScore(null) -> AnticheatScore(value)` after Analyzer success.
- `Scored stream -> deleted` when cleanup is enabled; otherwise preserved.

## 7. Conflicting state cleanup

Anti-cheat does not change pending or active-duel state. Finish removes actions
for non-accepted user/task pairs, including the opponent's actions for a task
accepted only by the other user. Scoring cleanup removes only the exact stream
being scored. Existing non-null scores are skipped.

## 8. Emitted messages

This process creates no client message and no outbox row for Analyzer. Analyzer
is called directly from the background handler. Suspicion scores are persisted
silently; no Frontend notification is defined.

## 9. Idempotency

- Action save is not idempotent: repeated `EventId` values are allowed.
- Finished/missing-duel actions are silently ignored, which is convergent but
  does not tell the client which actions were discarded.
- A non-null score is skipped, making completed scoring state-idempotent.
- Concurrent workers can call Analyzer for the same null score more than once.
  Last successful database write wins unless another conflict intervenes.

## 10. Transaction boundaries

- Each accepted action batch uses one save.
- Score creation and irrelevant-action deletion are part of the explicit duel
  finish transaction.
- Analyzer calls happen outside a database transaction started by this handler;
  all score assignments and optional deletions use one final save.
- If any call in a batch fails, earlier returned predictions are not saved and
  staged action deletions are not committed.

## 11. Concurrency and race conditions

- Action save can observe `InProgress`, then insert after a concurrent finish
  has selected/deleted actions, leaving late actions after scoring work is created.
- Multiple action retries create duplicate events and distort features.
- Multiple anti-cheat job instances can select the same null scores and duplicate
  Analyzer calls because there is no claim/lock.
- If cleanup is enabled, two workers can score against different action snapshots.
- A later Analyzer failure discards earlier work in the 20-row batch and causes
  repeated calls on the next iteration.
- Concurrent finish handlers can contend on the composite score key.

## 12. Failure handling

- Mixed-user batch: forbidden and saves none.
- No eligible active duel: success with no rows.
- Invalid duel participant/task is not rejected if the duel is active.
- Missing relation between score user and duel users returns not found and stops the batch.
- Analyzer failure stops the batch; the hosted service logs and retries later.
- Cancellation before the final save leaves scores null and actions intact.

## 13. User-visible result

Action upload returns success even when every action was filtered. No scoring or
cleanup state is pushed to Frontend. The score is an internal persisted result;
raw action retention depends on configuration.

## 14. Implementation references

- [Create.cs (user actions)](../../Duely/src/Duely.Application.UseCases/Features/UserActions/Create.cs)
- [CheckDuelsForFinish.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/CheckDuelsForFinish.cs)
- [CheckDuelsForAnticheat.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/CheckDuelsForAnticheat.cs)
- [AnticheatBackgroundService.cs](../../Duely/src/Duely.Application.BackgroundJobs/AnticheatBackgroundService.cs)
- [AnticheatScoresConfiguration.cs](../../Duely/src/Duely.Infrastructure.DataAccess.EntityFramework/Configurations/AnticheatScoresConfiguration.cs)
- [UserActionsConfiguration.cs](../../Duely/src/Duely.Infrastructure.DataAccess.EntityFramework/Configurations/UserActionsConfiguration.cs)
- [SaveUserActionsHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/SaveUserActionsHandlerTests.cs)
- [CheckDuelsForFinishHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CheckDuelsForFinishHandlerTests.cs)
- [CheckDuelsForAnticheatHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CheckDuelsForAnticheatHandlerTests.cs)

Tests cover filtering finished-duel actions, accepted-stream retention, score
assignment, non-null skip, batch reachability beyond older scored duels, and the
cleanup option. They do not cover duplicates, participant authorization, races,
partial batch failure, or multiple workers.

## 15. Open questions

- Should action upload verify duel participation and task existence?
- Is `EventId` intended as an idempotency key?
- Should all participants' streams be retained for an accepted task, or only the
  user who produced an Accepted submission?
- Is all-or-nothing persistence across 20 Analyzer calls intended?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Enforce a unique event identity and participant/task authorization.
- Claim score rows or use optimistic concurrency to prevent duplicate scoring.
- Persist each prediction independently, or explicitly retain batch atomicity
  with a stated retry/cost policy.
- Define retention and late-action behavior around duel finish.
