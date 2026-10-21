# Friendly duel invitations

## 1. Purpose

Allow one user to invite another directly, optionally with a duel configuration,
then accept, deny, or cancel that invitation.

## 2. Participants

- Sender (`FriendlyPendingDuel.User1`) and invitee (`User2`).
- `DuelInvitationsController` and the Friendly invitation handlers.
- Ranked search, disconnect cleanup, and the duel-making job.
- PostgreSQL, transactional outbox, and the participants' WebSockets.

## 3. Triggers

- `GET /duels/invitations`: list unaccepted incoming invitations.
- `POST /duels/invitations`: create an outgoing invitation.
- `POST /duels/invitations/accept`, `/deny`, or `/cancel`.
- `POST /duels/search` and `/duels/cancel`: change only Ranked search.
- Final WebSocket disconnect: legacy shared cleanup after a grace period.
- `DuelMakingJob`: turns an accepted invitation into a duel.

## 4. Preconditions

- Create: sender and opponent exist and differ. Optional configuration must
  exist; ownership is not checked here. Active duels do not block invitation creation.
- Accept: invitee and named sender exist, and a directional row for that pair
  exists. Active duels do not block acceptance.
- Deny/cancel: both users and the directional row for that pair must exist,
  except cancel treats a missing row as success. The optional `configuration_id`
  in these requests is accepted for compatibility but does not select a row.
- There is no uniqueness constraint for sender, pair, or configuration.

## 5. Current behavior

### Create

The handler validates sender, opponent, and optional configuration. An existing
outgoing row for the same opponent returns success without a
new message. Otherwise it creates a directional `IsAccepted = false` row and
records one `DuelInvitation` for the invitee. Other invitations, Ranked search,
and active duels remain unchanged.

The opposite-direction invitation is not a duplicate: tests explicitly preserve
both `A -> B` and `B -> A` rows.

### Accept

The incoming row for the pair is marked `IsAccepted = true` and saved once. Other
pending rows and active duels remain unchanged.
No acceptance message is emitted. The making job observes the flag later.

### Deny and sender cancel

- Deny deletes the incoming row and sends `DuelInvitationDenied` only to the sender.
- Sender cancel deletes the row and sends `DuelInvitationCanceled` to both users.
- Both notifications report the configuration stored on the deleted row, even
  when the request contains a different `configuration_id`.
- Sender cancel succeeds without messages when the row is already absent and
  also deletes an already accepted row if it has not yet been consumed.

### Listing and disconnect

The incoming list returns only rows where the caller is `User2` and
`IsAccepted == false`, ordered by id. Accepted invitations disappear from the
list but remain pending. Shared cleanup deletes all outgoing rows with messages;
incoming rows and their acceptance remain unchanged.

## 6. State transitions

- `No outgoing row -> Friendly(User1 -> User2, IsAccepted = false)`.
- `Outgoing A -> B; outgoing A -> C` when another invitation is created.
- `Incoming Friendly(false) -> Friendly(true)` on accept.
- `Incoming Friendly -> deleted` on deny.
- `Outgoing Friendly -> deleted` on sender cancel or disconnect.
- `Incoming Friendly(true) -> Friendly(true)` on invitee disconnect.
- `Friendly(true) -> deleted; Duel(InProgress) created` when selected.

## 7. Conflicting state cleanup

| Action | Ranked | Other outgoing Friendly | Incoming Friendly | Group | Tournament |
| --- | --- | --- | --- | --- | --- |
| Create | Unchanged | Preserve all; same directional pair is idempotent regardless of configuration | Unchanged | Unchanged | Unchanged |
| Accept | Unchanged | Unchanged | Mark requested row accepted | Unchanged | Unchanged |
| Deny | Unchanged | Unchanged | Delete requested row | Unchanged | Unchanged |
| Sender cancel | Unchanged | Delete requested row | Unchanged | Unchanged | Unchanged |
| Explicit search cancel | Delete caller Ranked | Unchanged | Unchanged | Unchanged | Unchanged |
| Disconnect cleanup | Delete all caller Ranked | Delete all and notify both sides | Keep all, including acceptance | Keep all, including acceptance | Keep all, including acceptance |
| Duel creation | Unchanged except selected row | Delete only selected row | Delete only selected row | Unchanged | Unchanged |

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| New invitation | `DuelInvitation` | Invitee | Sender nickname, configuration id | Show incoming invitation |
| Sender cancel or outgoing disconnect cleanup | `DuelInvitationCanceled` | Invitee and sender | Other user's nickname, configuration | Close both views |
| Deny | `DuelInvitationDenied` | Original sender only | Denier nickname, configuration | Report refusal |
| Accepted row becomes a duel | `DuelStarted` | Both duel participants | Duel id | Enter active duel |

All are outbox messages. There is deliberately no acceptance message. Rows are
recorded atomically with the state change in their handler, except `DuelStarted`
uses the second save inside the atomic duel-creation transaction. Delivery can repeat after an
outbox retry, while offline delivery is treated as success and cannot retry.

## 9. Idempotency

- Create to the same nickname returns success without a new row or message,
  even when another configuration is requested. The first pending row retains
  its configuration.
- Sequential sender cancel is idempotent: later calls succeed silently.
- Repeating accept before the row is consumed writes `true` again; after duel
  creation it returns not found.
- Repeating deny returns not found after the first success.
- Concurrent create/accept/cancel calls can duplicate rows, messages, or act on
  stale state because no request id, unique key, or row claim exists.

## 10. Transaction boundaries

- Each successful create, accept, deny, cancel, or shared cleanup uses one
  `SaveChangesAsync`; its state changes and outbox rows are atomic.
- Validation failures and duplicate returns occur before saving.
- Friendly-to-duel conversion has the pair transaction documented in
  [Duel lifecycle](duel-lifecycle.md#10-transaction-boundaries).

## 11. Concurrency and race conditions

- Two concurrent creates can both observe no matching pair and insert
  duplicates. Accept/cancel/deny select the oldest directional row; duplicates may
  remain until separately consumed or canceled.
- Accept and sender cancel can race. Either the cancel commits first and accept
  finds nothing, or accept can set a row that cancel subsequently deletes.
- The maker locks both users and checks the actual rated configuration of the
  proposed and active duels before insertion.
- A maker reloads `IsAccepted` after locking the selected pending row; explicit
  denial or cancellation of that row prevents creation.
- Accepted Friendly has priority over Group/Tournament/Ranked for rated slot
  selection in one maker call. Unrated rows can be selected concurrently.
- Mutual invitations are legal; accepting one preserves the opposite-direction row.

## 12. Failure handling

- Missing users/configuration/invitation produce not-found results as described above.
- Self-invitation is forbidden. Active duels do not reject create or accept.
- A missing row on sender cancel is successful; a missing row on accept/deny is an error.
- A failed request leaves existing invitations unchanged.
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

- Should concurrent duplicate create use a durable unique constraint?
- Is a durable acceptance/denial history required, or is row deletion sufficient?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Add a uniqueness key for each directional pair so concurrent creates cannot
  produce duplicate invitations with different configurations.
- Add request idempotency for state-changing invitation endpoints.
- Specify symmetric recipients for every cleanup path and test both participants.
- Consider a durable invitation uniqueness key; duel conversion already locks
  and re-checks invitation acceptance and rated-duel availability.
