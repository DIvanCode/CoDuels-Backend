# Duel lifecycle

## 1. Purpose

Convert ready pending processes into an active duel, expose live task/solution
state, determine the result, update ratings, and atomically schedule finish
notifications and anti-cheat work.

## 2. Participants

- Two users, Frontend, and all four typed pending processes.
- `DuelMakingJob`, `DuelManager`, `TryCreateDuelHandler`, Taski, and `TaskService`.
- WebSocket `SolutionUpdated` handling and `UpdateDuelTaskSolutionHandler`.
- `DuelEndWatcherJob`, `CheckDuelsForFinishHandler`, and `RatingManager`.
- PostgreSQL, outbox delivery, tournament/group linkage, and anti-cheat processing.

## 3. Triggers

- Periodic duel-making command (1 second in production).
- WebSocket `SolutionUpdated` events during or after a duel.
- Submission status changes from Taski.
- Periodic finish check (1 second in production).
- HTTP GET endpoints for active/history/detail recovery.

## 4. Preconditions

- A pending pair must satisfy its type-specific readiness rule.
- Taski must return tasks and `TaskService` must select a task for every
  configured key.
- No database invariant enforces distinct users or one active duel per user.
- The maker does not re-check active-duel status or lock pending rows.

## 5. Current behavior

### Selection and task assignment

One maker invocation selects every non-conflicting accepted Friendly pair, then
Group, then Tournament, plus at most one Ranked pair. `usedUsers` prevents a user
appearing twice only in that in-memory selection. For a pending row without a
configuration, the maker creates a one-task sequential configuration: Ranked is
rated, other types unrated, opponent solution visibility is enabled, duration is
the configured default, and level derives from current average rating.

Taski's full task list is fetched separately for each pair. Tasks from either
user's previous duels are preferred for exclusion. Chosen task ids are reserved
across pairs in this invocation; if too few unreserved tasks remain, the full
list is reused. Topic match, then level proximity, guides selection.

### Creation and linkage

The maker sets `StartTime`, `DeadlineTime`, current initial ratings, empty
solution dictionaries, and status `InProgress`. It deletes only the selected
`UsedPendingDuels` and saves the duel. Then it creates a `GroupDuel` link or
attaches the duel id to Tournament state when applicable, records
`DuelStarted` for both users, and saves again.

### Live solution state

The WebSocket accepts `SolutionUpdated` with duel id, one-character task key,
language, and solution. A participant's JSON solution dictionary is replaced
with an updated copy. If `ShouldShowOpponentSolution`, an
`OpponentSolutionUpdated` outbox row is recorded for the opponent in the same
save. The handler checks participant and task existence, but not duel status or
sequential task visibility; identical repeats still produce messages.

### Finish

Each finish command processes at most the first candidate ordered by deadline.
A duel is a candidate after its deadline or after any Done/Accepted submission.
For each task, the earliest Done/Accepted submission at or before deadline wins,
unless an earlier-or-equal not-Done submission still exists. The duel finishes
early only when every task has a winner. At deadline it waits while any
pre-deadline submission is not Done, then the user winning more tasks wins; an
equal count is a draw.

Finishing sets status/end/winner, updates ratings from stored initial ratings,
records two `DuelFinished` rows, creates one null anti-cheat score for every
distinct user/task with an accepted submission, and deletes actions for other
user/task combinations. All finish changes commit in one explicit transaction.

## 6. State transitions

- `Ready PendingDuel(s) -> deleted`.
- `No active record -> Duel(InProgress)`.
- `No stored task solution -> DuelTaskSolution`; later events replace its values.
- `InProgress -> Finished` when all tasks are solved or a non-blocked deadline passes.
- `User.Rating -> final rating` for rated duels; unrated final rating equals initial.
- `No AnticheatScore -> AnticheatScore(Score = null)` only for accepted user/task pairs.

There is no canceled, aborted, failed-to-start, or force-finished duel state.

## 7. Conflicting state cleanup

Creation removes only the selected pending rows. It does not remove Ranked,
Friendly, Group, or Tournament rows involving either user unless those exact
rows are in `UsedPendingDuels`. Finish does not clean any pending state. WebSocket
disconnect uses the separate shared cleanup and does not affect an active duel.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| Duel second creation save | `DuelStarted` | Both participants | Duel id | Fetch/open active duel |
| Visible opponent solution updated | `OpponentSolutionUpdated` | Opponent | Duel id, task key, solution, language | Live code view |
| Accepted submission status | `DuelChanged` | Both participants | Duel id | Refresh tasks/score |
| Duel finish transaction | `DuelFinished` | Both participants | Duel id | Fetch final result |

`DuelStarted` is not atomic with initial duel insertion. Live-solution and finish
messages are atomic with their business state. `DuelChanged` is documented in
[Submission and testing](submission-and-testing.md). Any message may be lost to
an offline socket or duplicated by outbox replay.

## 9. Idempotency

- Maker idempotency relies on pending deletion; there is no pair/request key.
- Identical solution events overwrite with identical data and create another message.
- Once a finish handler observes persisted `Finished`, it no longer selects that
  duel. Concurrent handlers can both load `InProgress` before either commits.
- Rating calculation is deterministic from initial ratings and winner, but
  finish notifications can duplicate under concurrent processing.

## 10. Transaction boundaries

- Taski lookup is outside a database transaction.
- Save 1: selected pending deletion, synthesized configuration, active duel.
- Save 2: group/tournament linkage and both `DuelStarted` outbox rows.
- Each live solution update and optional message use one save.
- Finish opens an explicit transaction: status/rating save, then messages,
  anti-cheat rows/action cleanup save, then commit. Those finish effects are atomic.
- No external delivery or Analyzer call occurs in the finish transaction.

## 11. Concurrency and race conditions

- Multiple maker instances can select the same pending rows; no transactional claim exists.
- Surviving pending rows can create a second active duel for an already-active user.
- Cancellation/reset after maker load may not prevent creation.
- Split creation can expose an active duel without linkage/start messages.
- Two finish handlers can process the same duel. With accepted tasks, the
  composite anti-cheat score key may make one transaction fail; without scores,
  duplicate finish messages can commit.
- A partial Accepted candidate can be repeatedly selected without finishing;
  later-deadline fully solved candidates can be delayed until the first candidate changes.
- A pre-deadline submission stuck nonterminal blocks finish indefinitely.
- `SolutionUpdated` can race with finish and still modify a finished duel.

## 12. Failure handling

- No pair: successful no-op.
- Taski/list/selection failure: failed command; the current pair stays pending,
  but pairs saved earlier in the loop remain active.
- Failure after save 1: no automatic compensation for active duel/pending deletion.
- Missing duel/task or nonparticipant solution update: not-found/forbidden and no save.
- Cancellation during finish rolls back the explicit transaction if commit did not complete.
- Finish messages can be inserted already expired when a duel finishes more than
  five minutes after its deadline.

## 13. User-visible result

`DuelStarted`, `DuelChanged`, live opponent solution, and `DuelFinished` prompt
the Frontend to update or fetch state. Recovery endpoints remain necessary
because push delivery is not guaranteed. Sequential configuration hides future
task ids until earlier winners are known; spectators see all tasks and both
solution dictionaries, while participant opponent solutions depend on configuration.

## 14. Implementation references

- [Duel.cs](../../Duely/src/Duely.Domain.Models/Duels/Duel.cs)
- [DuelManager.cs](../../Duely/src/Duely.Domain.Services/Duels/DuelManager.cs)
- [TaskService.cs](../../Duely/src/Duely.Domain.Services/Duels/TaskService.cs)
- [TryCreateDuel.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/TryCreateDuel.cs)
- [UpdateDuelTaskSolution.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/UpdateDuelTaskSolution.cs)
- [CheckDuelsForFinish.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/CheckDuelsForFinish.cs)
- [DuelDtoMapper.cs](../../Duely/src/Duely.Application.UseCases/Helpers/DuelDtoMapper.cs)
- [TryCreateDuelHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/TryCreateDuelHandlerTests.cs)
- [UpdateDuelTaskSolutionHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/UpdateDuelTaskSolutionHandlerTests.cs)
- [CheckDuelsForFinishHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CheckDuelsForFinishHandlerTests.cs)
- [TaskServiceTests.cs](../../Duely/tests/Duely.Domain.Tests/TaskServiceTests.cs)

Tests cover priority/gating, pending deletion, messages, task reuse avoidance,
visibility-conditioned live messages, deadline blocking, task winners, ratings
invocation, and anti-cheat row/action creation. They do not cover maker/finisher concurrency.

## 15. Open questions

- Is one active duel per user a required invariant?
- Should active duel creation, linkage, and start notifications be one transaction?
- Should solution updates be allowed for finished or hidden tasks?
- What timeout resolves permanently Running submissions?
- Should an early partial Accepted candidate be excluded until it can finish?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Introduce a transactional pending claim and a database active-user invariant.
- Make all activation effects one commit or add an explicit recoverable activation state.
- Reject/define live updates outside active visible tasks.
- Add a terminal policy for lost testing status and a fair finish-job query.
- Add concurrency tests against PostgreSQL for maker and finish jobs.
