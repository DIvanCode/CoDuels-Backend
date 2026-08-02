# User connection lifecycle

## 1. Purpose

Authenticate a WebSocket with an intended single-use ticket, maintain every
registered socket per user in a Duely process, relay client events to each open
socket, and attempt pending-duel cleanup only after the final local connection
has stayed disconnected through a reconnect grace period.

## 2. Participants

- Authenticated user/Frontend and `UsersController`.
- Ticket handlers, `UserWebSocketHandler`, and `WebSocketConnectionManager`.
- `WebSocketMessageSender`, `CancelPendingDuelsHandler`, and all pending types.
- ASP.NET request cancellation and the process-local in-memory connection map.

## 3. Triggers

- `POST /users/ticket`: generate/replace the user's ticket.
- WebSocket upgrade at `GET /users/connect?ticket=...`.
- Text `SolutionUpdated` frames.
- Client close frame, network failure, request cancellation, or server shutdown.

## 4. Preconditions

- Ticket creation requires an authenticated existing user.
- Connect requires an exact stored ticket and must be a WebSocket upgrade. The
  handler reads, clears, and saves the ticket without an atomic consume lock.
- Ticket storage has no unique database index or transactional consume lock.
- Connection registration is process-local; no distributed presence exists.

## 5. Current behavior

### Ticket and connect

Ticket creation generates 32 random bytes as hex, checks for collision up to
three times, stores it on the user, and returns it. Connect looks up the ticket,
sets it to null, saves, accepts the WebSocket, then works with the user id. Two
concurrent handlers can both read the same value before either clearing save is
committed, so this does not prove exactly-one consumption.

### Registering connections

After accepting a socket, the handler registers it with a new `connectionId`.
Several local sockets may be registered for the same user; a new tab/reconnect
does not close or replace an existing tab. The connection manager keeps the
`connectionId` to user and socket mappings under one lock and returns a snapshot
of all sockets when sending a notification.

### Receive loop

While the request token is not canceled and the socket is open, close frames
break the loop, non-text frames are ignored, and fragmented text is reassembled.
Invalid/unknown JSON is logged and ignored. A valid `SolutionUpdated` invokes a
use case; business failure is logged and no acknowledgment is sent.

### `finally` cleanup

On loop exit or exception, the handler creates an independent close token. If
the current socket still reports `Open`, it attempts a normal close; a close
failure is logged and followed by `Abort` so the registration cleanup still
runs. It removes only its own `connectionId` from the connection manager.

Only the handler that removes the final local socket receives a disconnect
token. It waits the configured 10-second reconnect grace period with a separate
30-second cleanup token, then sends `CancelPendingDuelsCommand` only if the
same token still marks the user as disconnected. A reconnect invalidates that
token; a later last disconnect receives a new token, so an older timer cannot
shorten the later grace period. That command:

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
- `No registered socket -> user id maps to one connection id and socket`.
- `One or more registered sockets -> another connection id and socket is added`.
- `Handler ends -> only its connection id is removed`.
- `Final connection removed -> disconnect token -> 10-second grace -> pending
  transitions below, unless the token is invalidated by a reconnect`.
- There is no durable `Online -> Offline` transition.

## 7. Conflicting state cleanup

| State | Cleanup after the final local connection stays absent for the grace period |
| --- | --- |
| Ranked | Delete all; no message |
| Outgoing Friendly | Delete all; cancellation to sender and each invitee |
| Incoming Friendly | Preserve; reset invitee acceptance; no message |
| Group | Preserve; reset only disconnected user's acceptance; no message |
| Tournament | Preserve; reset only disconnected user's acceptance; no message |
| Active duel, submissions, code runs | Unchanged |

The same broad cleanup is attempted only after normal close, network failure,
request cancellation, or an exception has removed the last local socket and no
reconnect invalidates its token during the grace period. A close failure does
not skip map removal or the grace-period decision.

## 8. Emitted messages

Connection/offline transitions emit no dedicated client message. A server
notification is attempted for every currently open local socket of its
recipient; non-open sockets are skipped, and sends to different sockets are
started independently so a slow socket does not block the others.
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
- `RemoveConnection(connectionId)` is identity-aware. Repeating it is a no-op,
  and an old handler cannot remove a sibling connection.
- Each final removal creates one cleanup token. Reconnect invalidates it, while
  cleanup completion removes it only when it is still current.
- Reconnecting always replaces the stored ticket, but adds another registered
  socket rather than replacing one.

## 10. Transaction boundaries

- Ticket generation/replace is one save; ticket consume is a separate save.
- WebSocket accept/registration is in memory and outside PostgreSQL.
- Disconnect cleanup uses one save for all pending state and its Friendly outbox rows.
- Physical close and cleanup are not atomic. A reconnect between the final
  removal and grace-period check invalidates cleanup; a reconnect after the
  check can still race with the database command.

## 11. Concurrency and race conditions

- An old handler can remove only its own registration; it cannot remove a newer
  or sibling socket.
- Multiple tabs are deliberately retained. Only the final local disconnect
  starts cleanup; reconnect/new-last-disconnect cycles use distinct tokens.
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
- Current-socket close failure is logged and aborted before registration cleanup.
- Cleanup failure is logged, and its disconnect token is removed when that
  cleanup attempt ends.
- `WebSocketMessageSender` swallows send exceptions and skips absence/non-open
  sockets while continuing delivery to open siblings.

## 13. User-visible result

Every currently open tab connected to the same Duely instance should receive a
notification. There is no backend online/offline event and no cross-instance
socket routing. The Frontend must obtain a new ticket to reconnect and recover
active/invitation/submission state through HTTP because messages during
disconnection are not replayed.

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
- [WebSocketServicesTests.cs](../../Duely/tests/Duely.Infrastructure.Api.Http.Tests/WebSocketServicesTests.cs)

Ticket and cleanup handlers have focused tests. The WebSocket service tests
cover retaining/removing sibling registrations, invalidating stale disconnect
cleanup, and fan-out to every open socket. Handler-level integration coverage
for close failure and the grace-period timer is still absent.

## 15. Open questions

- Should replacement count as a business disconnect when a new connection exists?
- Is online/offline intended to be durable or observable by other users?
- Is Duely guaranteed to run one replica, or must WebSocket routing be distributed?
- Should unaccepted incoming/Group/Tournament invitations survive disconnect?
- See [Open questions](open-questions.md).

## 16. Proposed requirements

- Define multi-instance routing/replay and ticket-consumption atomicity.
- Add integration tests for normal close, abort, cancellation, reconnect inside
  and after the grace period, and multiple tabs.
