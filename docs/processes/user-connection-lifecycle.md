# User connection lifecycle

## 1. Purpose

Authenticate a WebSocket with a one-time ticket, maintain one registered socket
per user in a Duely process, relay client events, and apply pending-duel cleanup
when a connection handler ends.

## 2. Participants

- Authenticated user/Frontend and `UsersController`.
- Ticket handlers, `UserWebSocketHandler`, and `WebSocketConnectionManager`.
- `WebSocketMessageSender`, `CancelPendingDuelsHandler`, and all pending types.
- ASP.NET request cancellation and the process-local in-memory connection map.

## 3. Triggers

- `POST /users/ticket`: generate/replace the user's ticket.
- WebSocket upgrade at `GET /users/connect?ticket=...`.
- Text `SolutionUpdated` frames.
- Client close frame, network failure, request cancellation, server shutdown, or
  replacement by a new connection.

## 4. Preconditions

- Ticket creation requires an authenticated existing user.
- Connect consumes an exact stored ticket and must be a WebSocket upgrade.
- Ticket storage has no unique database index or transactional consume lock.
- Connection registration is process-local; no distributed presence exists.

## 5. Current behavior

### Ticket and connect

Ticket creation generates 32 random bytes as hex, checks for collision up to
three times, stores it on the user, and returns it. Connect looks up the ticket,
sets it to null, saves, accepts the WebSocket, then works with the user id.

### Replacing a connection

After accepting the new socket, the handler gets the currently registered one.
If present, it removes the dictionary entry and tries to close the old socket as
`NormalClosure` with reason `Replaced by new connection`; timeout/failure falls
back to abort. Only after awaiting that close does it register the new socket.

### Receive loop

While the request token is not canceled and the socket is open, close frames
break the loop, non-text frames are ignored, and fragmented text is reassembled.
Invalid/unknown JSON is logged and ignored. A valid `SolutionUpdated` invokes a
use case; business failure is logged and no acknowledgment is sent.

### `finally` cleanup

On loop exit or exception, the handler creates an independent close token. If
the current socket still reports `Open`, it attempts a normal close. It then
blindly removes the user id from the connection map and, with a separate
30-second token, sends `CancelPendingDuelsCommand`. That command:

- deletes all Ranked rows for the user;
- deletes all outgoing Friendly rows and records cancellation for both sides;
- keeps incoming Friendly rows but sets `IsAccepted = false`;
- keeps every involving Group/Tournament row but resets only this user's flag.

No persistent online/offline property is changed. Physical close, removal from
the process map, and business cleanup are three distinct effects, even though
the code does not model them as separate states.

If the current socket's `CloseAsync` throws in `finally`, there is no catch around
that call; map removal and business cleanup below it can be skipped.

## 6. State transitions

- `AuthTicket = old/null -> random ticket`.
- `Stored ticket -> null` on successful lookup/save.
- `No registered socket -> user id maps to socket`.
- `Old socket registered -> entry removed -> old closed/aborted -> new registered`.
- `Handler ends -> entry removed` (without checking socket identity).
- Pending transitions are the type-specific cleanup listed above.
- There is no durable `Online -> Offline` transition.

## 7. Conflicting state cleanup

| State | Disconnect cleanup |
| --- | --- |
| Ranked | Delete all; no message |
| Outgoing Friendly | Delete all; cancellation to sender and each invitee |
| Incoming Friendly | Preserve; reset invitee acceptance; no message |
| Group | Preserve; reset only disconnected user's acceptance; no message |
| Tournament | Preserve; reset only disconnected user's acceptance; no message |
| Active duel, submissions, code runs | Unchanged |

The same broad cleanup runs for normal close, network failure, request
cancellation, replacement, and normally any exception. It is not conditioned on
whether another socket for that user is already active.

## 8. Emitted messages

Connection/replacement/offline transitions emit no dedicated client message.
Cleanup can create `DuelInvitationCanceled` for each outgoing Friendly row, sent
to both users with their opposite nickname and configuration. Those rows commit
with deletions in one save. Acceptance resets emit nothing.

Messages delivered *through* a WebSocket are described in
[Notifications and outbox](notifications-and-outbox.md); absence/closed state
causes silent successful delivery from the outbox's perspective.

## 9. Idempotency

- A ticket is intended for one use, but concurrent consumers can both read it
  before either clears it.
- Shared pending cleanup is mostly convergent; outgoing Friendly messages are
  emitted only for rows present in that invocation.
- `RemoveConnection(userId)` is not identity-aware and is therefore not
  idempotent with respect to an old and new socket: an old handler can remove the new entry.
- Reconnecting always replaces the stored ticket and registered connection.

## 10. Transaction boundaries

- Ticket generation/replace is one save; ticket consume is a separate save.
- WebSocket accept/registration is in memory and outside PostgreSQL.
- Disconnect cleanup uses one save for all pending state and its Friendly outbox rows.
- Physical close and cleanup are not atomic. Cleanup can run even after a new
  connection is registered, or be skipped if close throws first.

## 11. Concurrency and race conditions

- Old connection replacement can finish its own `finally` after the new socket
  is registered, remove the new dictionary entry, and cancel newly created pending state.
- Multiple tabs deliberately replace each other; the old tab's cleanup is not
  generation-aware, so replacement is a business disconnect as well as a physical close.
- Two simultaneous connects can both see/consume the same ticket or both manipulate
  the same dictionary entry without a compare-and-remove operation.
- In a multi-instance deployment, each instance can hold a socket for the same
  user. An outbox row claimed by another instance sees no local socket and is deleted.
- Disconnect and duel making can race; stale accepted rows can still produce a duel.
- Request cancellation while the socket remains open can make `CloseAsync` time
  out and prevent the cleanup statements below it.

## 12. Failure handling

- Invalid/used ticket: authentication error before upgrade.
- Non-WebSocket request: bad request after ticket has already been consumed.
- Invalid/unknown frames: logged and ignored without closing.
- Receive/send cancellation or network failure reaches `finally`, unless process termination prevents it.
- Existing-socket close failure is caught and aborted; current-socket close failure is not caught.
- Cleanup failure is logged, but connection teardown continues when the code reaches it.
- `WebSocketMessageSender` swallows send exceptions and absence/non-open states.

## 13. User-visible result

Only the newest intended tab should receive messages, but races can leave no
registered socket or more than one physical socket across instances. There is no
backend online/offline event. The Frontend must obtain a new ticket to reconnect
and recover active/invitation/submission state through HTTP because messages
during disconnection are not replayed.

## 14. Implementation references

- [UsersController.cs](../../Duely/src/Duely.Infrastructure.Api.Http/Controllers/UsersController.cs)
- [CreateTicket.cs](../../Duely/src/Duely.Application.UseCases/Features/Users/CreateTicket.cs)
- [GetByTicket.cs](../../Duely/src/Duely.Application.UseCases/Features/Users/GetByTicket.cs)
- [UserWebSocketHandler.cs](../../Duely/src/Duely.Infrastructure.Api.Http/Services/WebSockets/UserWebSocketHandler.cs)
- [WebSocketConnectionManager.cs](../../Duely/src/Duely.Infrastructure.Api.Http/Services/WebSockets/WebSocketConnectionManager.cs)
- [WebSocketMessageSender.cs](../../Duely/src/Duely.Infrastructure.Api.Http/Services/WebSockets/WebSocketMessageSender.cs)
- [CancelPendingDuels.cs](../../Duely/src/Duely.Application.UseCases/Features/Duels/CancelPendingDuels.cs)
- [AuthTicketHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/AuthTicketHandlerTests.cs)
- [CancelPendingDuelsHandlerTests.cs](../../Duely/tests/Duely.Application.Tests/Handlers/CancelPendingDuelsHandlerTests.cs)

Ticket and cleanup handlers have focused tests. There are no tests for the
WebSocket handler, connection manager, replacement, multiple tabs, close failure,
or old-connection cleanup race.

## 15. Open questions

- Should replacement count as a business disconnect when a new connection exists?
- Is online/offline intended to be durable or observable by other users?
- Is Duely guaranteed to run one replica, or must WebSocket routing be distributed?
- Should unaccepted incoming/Group/Tournament invitations survive disconnect?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Associate each registration with a generation/token and compare-and-remove
  only the socket owned by the exiting handler.
- Skip business cleanup when a newer valid connection is active, if replacement
  is not meant to cancel pending state.
- Catch current-socket close failure so map removal and cleanup still run.
- Define multi-instance routing/replay and ticket-consumption atomicity.
- Add integration tests for normal close, abort, cancellation, replacement, and multiple tabs.
