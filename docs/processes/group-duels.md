# Group duels

## 1. Purpose

Allow an authorized group member to nominate two group users for a duel, collect
an independent acceptance from each participant, and retain a `GroupDuel` link
after the active duel is created.

## 2. Participants

- The actor creating/canceling the group duel, two nominated users, and their group.
- `GroupDuelInvitationsController` and create/accept/cancel/list handlers.
- `GroupPermissionsService`, `DuelManager`, and `DuelMakingJob`.
- PostgreSQL, outbox delivery, Taski, and participant WebSockets.

## 3. Triggers

- `POST /duels/group/invitations`: create a pending group duel.
- `GET /duels/group/invitations`: list invitations not yet accepted by the caller.
- `POST /duels/group/invitations/accept`: accept as one participant.
- `POST /duels/group/invitations/cancel`: authorized deletion of the pending pair.
- Final WebSocket disconnect: remove Ranked search and outgoing Friendly
  invitations, while preserving Group acceptance. Explicit
  `POST /duels/cancel` removes only Ranked search.
- `DuelMakingJob`: consume a row after both participants accept.

## 4. Preconditions

- The actor exists, belongs to the group, and `CanCreateDuel`/`CanCancelDuel`
  permits the requested action.
- Both nominated users exist and have memberships in the group when created.
- Nominated users may have active duels when the invitation is created.
- Optional configuration exists. Configuration ownership is not checked.
- Create does not reject `User1Id == User2Id`.
- Accept does not re-check membership or active duel state.

## 5. Current behavior

### Create and list

The handler loads the actor and a group with filtered memberships for the actor
and both nominated users, verifies permissions/memberships and the
configuration, then searches for the same group and unordered pair regardless
of configuration. An existing row returns success without messages. Otherwise it
creates `GroupPendingDuel` with both acceptance flags false and records one
`GroupDuelInvitation` for each participant. The list returns rows where the
caller's own flag is false, ordered by row id.

### Acceptance

The matching Group row is located by group and caller/opponent pair, then only
the caller's positional flag is set true. The optional request
`configuration_id` is accepted for compatibility but does not select a row. No group
acceptance message is emitted. Ranked search, other invitations, and active
duels remain unchanged.

### Cancellation and disconnect

An authorized group actor can delete the whole row; both participants receive a
`GroupDuelInvitationCanceled`. A missing row is successful. In contrast, shared
disconnect cleanup keeps the row and both acceptance flags, without a group
message. Cancellation notifications use the stored row configuration.

### Duel creation

`DuelManager` requires both flags. Accepted Friendly rows have higher priority;
accepted Tournament rows and Ranked have lower priority. On selection, the Group
row is locked and both acceptance flags are re-checked. A rated configuration
waits while either human has another active rated duel; an unrated configuration
can run alongside any active duel.
The row is deleted with the new duel, `GroupDuel { Group, Duel, CreatedBy }`
link, and two `DuelStarted` rows in one transaction.

## 6. State transitions

- `No matching row -> GroupPendingDuel(false, false)`.
- `User1 false -> true` or `User2 false -> true` on acceptance.
- `This user's true -> true` on disconnect cleanup.
- `GroupPendingDuel -> deleted` on authorized cancel.
- `GroupPendingDuel(true, true) -> deleted; Duel(InProgress) created`.
- `No GroupDuel link -> GroupDuel created` before the creation transaction commits.

## 7. Conflicting state cleanup

| State | Create | Participant accept | Authorized cancel | Disconnect cleanup |
| --- | --- | --- | --- | --- |
| Ranked | Unchanged for both nominees | Unchanged | Unchanged | Delete caller's all |
| Outgoing Friendly | Unchanged | Unchanged | Unchanged | Delete caller's all; notify both sides |
| Incoming Friendly | Unchanged | Unchanged | Unchanged | Keep, including acceptance |
| Other Group rows | Unchanged | Unchanged | Unchanged | Keep, including acceptance |
| Tournament rows | Unchanged | Unchanged | Unchanged | Keep, including acceptance |

Creating a Group row does not reserve participants against other pending types.
Tournament synchronization also allows other pending processes to coexist.

## 8. Emitted messages

| Condition | Message type | Recipient | Main data | Reason |
| --- | --- | --- | --- | --- |
| New Group row | `GroupDuelInvitation` | User1 | Group name, User2 nickname, configuration | Request acceptance |
| New Group row | `GroupDuelInvitation` | User2 | Group name, User1 nickname, configuration | Request acceptance |
| Authorized cancellation | `GroupDuelInvitationCanceled` | User1 and User2 | Group name, other nickname, configuration | Close invitation state |
| Group row becomes active duel | `DuelStarted` | Both participants | Duel id | Enter duel |

Creation/cancellation rows commit atomically with the Group pending transition.
Acceptance produces no Group message. `DuelStarted` and `GroupDuel` are recorded
after the active duel's first save but in the same transaction. General retry/duplicate behavior is in
[Notifications and outbox](notifications-and-outbox.md).

## 9. Idempotency

- Sequential duplicate create for the same unordered pair returns
  success with no extra row or message.
- Repeating acceptance while the row exists writes the same flag; after
  consumption it returns not found.
- Sequential authorized cancel is idempotent and silent after the first call.
- Disconnect cleanup can generate Friendly cancellation rows for unrelated
  outgoing invitations; it does not change Group acceptance.
- Concurrent duplicate create is not protected by a unique constraint.

## 10. Transaction boundaries

- Each create, accept, authorized cancel, and shared cleanup uses one save for
  state and outbox rows.
- Taski's catalog is fetched once before pair transactions. Duel conversion uses
  one pair transaction: pending deletion and `Duel` are saved first, then
  `GroupDuel` plus both `DuelStarted` rows, then both saves commit together.

## 11. Concurrency and race conditions

- Concurrent creates can insert duplicates and duplicate invitations.
- Both acceptances can race with authorized cancellation; the maker uses the values
  reloaded after locking the Group pending row.
- Authorized cancel and the maker serialize on the Group pending row.
- `TryCreateDuelHandler` locks both users and re-checks active rated duels for a
  rated Group configuration. An unrated Group row may be consumed beside any duel.
- If `User1 == User2`, create sends two messages to one user, but accept always
  takes the `User1` branch; `IsAcceptedByUser2` cannot be set through this handler.
- Failure between the two duel-creation saves rolls the transaction back; the
  Group row remains and no active duel, link, or start message is exposed.

## 12. Failure handling

- Missing actor/group/user/membership/configuration: not found, except permission
  failures return forbidden.
- Missing accept target: not found; missing cancel target: success.
- Validation failures leave other pending processes unchanged.
- Task-selection failure leaves the Group row pending and later pairs continue.
  Pair persistence failure leaves the row pending and stops the tick.
- Message delivery failure does not undo the committed pending transition.

## 13. User-visible result

Both nominees receive an invitation on creation and can recover unaccepted rows
through GET. A user's accepted row disappears from their GET list while the
other participant can still see it. There is no acceptance/reset event. Both
participants are expected to learn activation through `DuelStarted`; authorized
cancel has an explicit event, while disconnect leaves acceptance intact.

## 14. Implementation references

- [GroupPendingDuel.cs](../../Duely/src/Duely.Domain.Models/Duels/Pending/GroupPendingDuel.cs)
- [CreateGroupDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/CreateGroupDuelInvitation.cs)
- [AcceptGroupDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/AcceptGroupDuelInvitation.cs)
- [CancelGroupDuelInvitation.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/CancelGroupDuelInvitation.cs)
- [GetIncomingGroupDuelInvitations.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/Invitations/GetIncomingGroupDuelInvitations.cs)
- [GroupPermissionsService.cs](../../Duely/src/Duely.Domain.Services/Groups/GroupPermissionsService.cs)
- [CreateGroupDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CreateGroupDuelInvitationHandlerTests.cs)
- [AcceptGroupDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/AcceptGroupDuelInvitationHandlerTests.cs)
- [CancelGroupDuelInvitationHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CancelGroupDuelInvitationHandlerTests.cs)

Acceptance tests verify that Ranked and Friendly pending rows survive without
extra cancellation messages.

## 15. Open questions

- Must the two participants be distinct and still be group members at acceptance/start?
- Should each acceptance be visible to the other participant or group actor?
- Should Group membership also be re-checked during transactional duel creation?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Validate distinct participants and add a database uniqueness rule for the
  intended group/pair identity.
- Re-check membership at acceptance or start; rated availability and acceptance
  are already re-checked in the transactional pair claim.
- Specify symmetric cleanup recipients and acceptance events.
