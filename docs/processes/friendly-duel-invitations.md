# Friendly duel invitations

## 1. Purpose

Allow one user to invite another directly, optionally with a duel configuration,
then accept, deny, cancel, replace, or implicitly reset that invitation.

## 2. Participants

- Sender (`FriendlyPendingDuel.User1`) and invitee (`User2`).
- `DuelInvitationsController` and the Friendly invitation handlers.
- Ranked start, shared disconnect/cancel cleanup, and the duel-making job.
- PostgreSQL, transactional outbox, and the participants' WebSockets.

## 3. Triggers

- `GET /duels/invitations`: list unaccepted incoming invitations.
- `POST /duels/invitations`: create or replace an outgoing invitation.
- `POST /duels/invitations/accept`, `/deny`, or `/cancel`.
- `POST /duels/search`: may automatically cancel the caller's outgoing invitation.
- `POST /duels/cancel` and WebSocket termination: shared cleanup.
- `DuelMakingJob`: turns an accepted invitation into a duel.

## 4. Preconditions

- Create: sender and opponent exist, differ, and neither has an observed active
  duel. Optional configuration must exist; ownership is not checked here.
- Accept: invitee exists, has no observed active duel, the named sender exists,
  and a directional row with matching configuration exists.
- Deny/cancel: both users and the exact directional/configuration row must exist,
  except cancel treats a missing row as success.
- There is no uniqueness constraint for sender, pair, or configuration.

## 5. Current behavior

### Create and replace

1. The handler rejects an active sender duel and stages deletion of one sender
   Ranked row if present.
2. It loads one outgoing Friendly row. If its `User2.Nickname` equals the new
   nickname, it returns success immediately. Configuration is not compared and
   staged Ranked deletion is not saved.
3. For a different opponent, it stages deletion of the old row and two
   cancellation messages, then validates the new opponent and configuration.
4. It creates a new directional row with `IsAccepted = false` and records one
   `DuelInvitation` for the invitee. One save commits all staged work.

The opposite-direction invitation is not a duplicate: tests explicitly preserve
both `A -> B` and `B -> A` rows.

### Accept

The invitee's one Ranked row and one outgoing Friendly row are staged for
deletion. For that outgoing row, only the accepting user receives a
`DuelInvitationCanceled`; its former invitee receives nothing. The requested
incoming row is then marked `IsAccepted = true` and everything is saved once.
No acceptance message is emitted. The making job observes the flag later.

### Deny and sender cancel

- Deny deletes the incoming row and sends `DuelInvitationDenied` only to the sender.
- Sender cancel deletes the row and sends `DuelInvitationCanceled` to both users.
- Sender cancel succeeds without messages when the row is already absent and
  also deletes an already accepted row if it has not yet been consumed.

### Listing and disconnect

The incoming list returns only rows where the caller is `User2` and
`IsAccepted == false`, ordered by id. Accepted invitations disappear from the
list but remain pending. Shared cleanup deletes all outgoing rows with messages;
incoming rows remain but their acceptance is reset without messages.

## 6. State transitions

- `No outgoing row -> Friendly(User1 -> User2, IsAccepted = false)`.
- `Outgoing A -> B -> deleted; outgoing A -> C created` on replacement.
- `Incoming Friendly(false) -> Friendly(true)` on accept.
- `Incoming Friendly -> deleted` on deny.
- `Outgoing Friendly -> deleted` on sender cancel, Ranked start, or disconnect.
- `Incoming Friendly(true) -> Friendly(false)` on invitee cancel/disconnect.
- `Friendly(true) -> deleted; Duel(InProgress) created` when selected.

## 7. Conflicting state cleanup

| Action | Ranked | Other outgoing Friendly | Incoming Friendly | Group | Tournament |
| --- | --- | --- | --- | --- | --- |
| Create | Delete sender's one Ranked on saved path | Preserve same nickname regardless of config; replace a different nickname | Unchanged | Unchanged | Unchanged |
| Accept | Delete invitee's one Ranked | Delete invitee's one outgoing; notify only invitee | Mark requested row accepted | Unchanged | Unchanged |
| Deny | Unchanged | Unchanged | Delete requested row | Unchanged | Unchanged |
| Sender cancel | Unchanged | Delete requested row | Unchanged | Unchanged | Unchanged |
| Shared cancel/disconnect | Delete all caller Ranked | Delete all and notify both sides | Keep all; reset caller-as-invitee acceptance | Reset caller flag | Reset caller flag |
| Duel creation | Unchanged except selected row | Delete only selected row | Delete only selected row | Unchanged | Unchanged |

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| New invitation | `DuelInvitation` | Invitee | Sender nickname, configuration id | Show incoming invitation |
| Replacing an old outgoing invitation | `DuelInvitationCanceled` | Old invitee and sender | Other user's nickname, old configuration | Close both views |
| Sender cancel, Ranked start, or outgoing disconnect cleanup | `DuelInvitationCanceled` | Invitee and sender | Other user's nickname, configuration | Close both views |
| Accept deletes acceptor's separate outgoing invitation | `DuelInvitationCanceled` | Acceptor only | Former invitee nickname/configuration | Close acceptor's outgoing view |
| Deny | `DuelInvitationDenied` | Original sender only | Denier nickname, configuration | Report refusal |
| Accepted row becomes a duel | `DuelStarted` | Both duel participants | Duel id | Enter active duel |

All are outbox messages. There is deliberately no acceptance message. Rows are
recorded atomically with the state change in their handler, except `DuelStarted`
uses the second save inside the atomic duel-creation transaction. Delivery can repeat after an
outbox retry, while offline delivery is treated as success and cannot retry.

## 9. Idempotency

- Create to the same nickname returns success without a new row or message, but
  it also ignores a requested configuration change.
- Sequential sender cancel is idempotent: later calls succeed silently.
- Repeating accept before the row is consumed writes `true` again; after duel
  creation it returns not found.
- Repeating deny returns not found after the first success.
- Concurrent create/accept/cancel calls can duplicate rows, messages, or act on
  stale state because no request id, unique key, or row claim exists.

## 10. Transaction boundaries

- Each successful create, accept, deny, cancel, or shared cleanup uses one
  `SaveChangesAsync`; its state changes and outbox rows are atomic.
- Validation failures and early returns occur before saving, so any previously
  staged cleanup in that handler is discarded.
- Friendly-to-duel conversion has the pair transaction documented in
  [Duel lifecycle](duel-lifecycle.md#10-transaction-boundaries).

## 11. Concurrency and race conditions

- Two creates can both observe no outgoing row and insert duplicates; later
  `SingleOrDefaultAsync` calls can throw.
- Accept and sender cancel can race. Either the cancel commits first and accept
  finds nothing, or accept can set a row that cancel subsequently deletes.
- Accept can pass its HTTP-time active-duel check, but the maker locks both users
  and re-checks active state before insertion.
- A maker reloads `IsAccepted` after locking the selected pending row, so a reset
  committed before that reload prevents creation.
- Accepted Friendly has priority over Group/Tournament/Ranked in one maker call.
  Non-selected pending rows survive, but are skipped while either user has an
  active duel.
- Mutual invitations are legal. Accepting one can delete the opposite-direction
  row and notify only the acceptor, producing asymmetric client knowledge.

## 12. Failure handling

- Missing users/configuration/invitation produce not-found results as described above.
- Self-invitation is forbidden; observed active duel produces already-exists.
- A missing row on sender cancel is successful; a missing row on accept/deny is an error.
- If validation of a replacement target fails, deletion/messages staged for the
  old invitation are not saved, so the old invitation remains.
- Cancellation before `SaveChangesAsync` leaves durable state unchanged.
- Task-selection failure leaves the accepted invitation pending and does not
  stop later pairs. Pair persistence failure leaves it pending and stops the tick.
- Message delivery failure does not roll back the already committed invitation transition.

## 13. User-visible result

The incoming HTTP list is the recovery source for unaccepted invitations.
`DuelInvitation`, `DuelInvitationCanceled`, and `DuelInvitationDenied` are
backend facts intended to update client state, but the exact Frontend reducer was
not analyzed. Since acceptance emits no event, both clients learn that a duel
started only through `DuelStarted`; if that delivery is lost, they must recover
by fetching active duel state.

## 14. Implementation references

- [FriendlyPendingDuel.cs](../../Duely/src/Duely.Domain.Models/Duels/Pending/FriendlyPendingDuel.cs)
- [CreateDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/CreateDuelInvitation.cs)
- [AcceptDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/AcceptDuelInvitation.cs)
- [DenyDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/DenyDuelInvitation.cs)
- [CancelDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/CancelDuelInvitation.cs)
- [GetIncomingDuelInvitations.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/GetIncomingDuelInvitations.cs)
- [CreateDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CreateDuelInvitationHandlerTests.cs)
- [AcceptDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/AcceptDuelInvitationHandlerTests.cs)
- [CancelDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CancelDuelInvitationHandlerTests.cs)
- [DenyDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/DenyDuelInvitationHandlerTests.cs)
- [DuelInvitationsHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/DuelInvitationsHandlerTests.cs)

The last test file calls Friendly rows "ranked invitations" in two test names;
the assertions exercise `FriendlyPendingDuel`, so the names conflict with code.

## 15. Open questions

- Should same opponent with a different configuration replace the invitation?
- Should the former invitee be notified when acceptance deletes the acceptor's
  separate outgoing invitation?
- Should incoming accepted invitations coexist with Ranked search?
- Is a durable acceptance/denial history required, or is row deletion sufficient?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Define a uniqueness key and explicit semantics for pair, direction, and configuration.
- Add request idempotency for state-changing invitation endpoints.
- Specify symmetric recipients for every cleanup path and test both participants.
- Consider a durable invitation uniqueness key; duel conversion already locks
  and re-checks invitation acceptance and both users' active-duel status.
