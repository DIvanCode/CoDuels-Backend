# Glossary

## Core entities

| Term | Current meaning |
| --- | --- |
| `Duel` | A persisted match between exactly `User1` and `User2`, with configuration, tasks, initial/final ratings, solutions, submissions, timing, winner, and `InProgress`/`Finished` status. |
| `PendingDuel` | A TPH persistence base class for one of four pre-duel processes. It has no common cancellation or acceptance behavior beyond `Id`, `Type`, and `CreatedAt`. |
| `DuelConfiguration` | Rated flag, opponent-solution visibility, duration, task count/order, and task selection criteria. It may be referenced by a pending row or synthesized during duel creation. |
| `OutboxMessage` | A durable instruction to test a solution, run code, or attempt a client message. It is not the WebSocket payload itself. |
| Client `Message` | A polymorphic JSON payload sent toward one user through the current process's WebSocket connection. |

## Pending duel types

| Type | Participants and readiness | Default rating behavior |
| --- | --- | --- |
| `RankedPendingDuel` | One user, a rating snapshot, and creation time. Two compatible rows form a pair. | Always rated once paired. |
| `FriendlyPendingDuel` | Directional `User1` (sender) -> `User2` (invitee), optional configuration, one `IsAccepted` flag controlled by the invitee. | Configuration's `IsRated`; without a configuration, unrated. |
| `GroupPendingDuel` | Group, creator, two unordered participants, optional configuration, one acceptance flag per participant. | Configuration's `IsRated`; without a configuration, unrated. |
| `TournamentPendingDuel` | Tournament, two participants, tournament configuration, one acceptance flag per participant. Created by synchronization, not directly by HTTP. | Configuration's `IsRated`; without a configuration, unrated. |

`User1` and `User2` are positional for acceptance and message payloads. Friendly
invitations are directional; Group and Tournament pair matching treats the two
positions as an unordered pair.

## Duel and testing statuses

- `DuelStatus`: `InProgress` -> `Finished`. There is no canceled or failed duel status.
- `SubmissionStatus`: `Queued` -> `Running` -> `Done`. Terminal verdict is a free-form Taski string; `"Accepted"` has special business meaning.
- `UserCodeRunStatus`: `Queued` -> `Running` -> `Done`.
- `TournamentStatus`: `New` -> `InProgress` -> `Finished`.
- `OutboxStatus`: `ToDo` -> `InProgress`; failure changes it to `ToRetry`; success deletes the row.

## Connection terms

- **Physical WebSocket close**: a close frame, abort, network failure, request
  cancellation, replacement, or exception ends a handler loop.
- **Registered connection**: the socket stored under a user id in the current
  process's in-memory `WebSocketConnectionManager`.
- **Offline**: not a persisted domain state. The current code has no durable
  online/offline flag and cannot distinguish all physical-close and replacement
  races from business disconnect.
- **Business disconnect cleanup**: `CancelPendingDuelsCommand`, normally called
  from the WebSocket handler's `finally` block. It is broader than canceling
  Ranked search.

## Delivery terms

- **Recorded atomically** means the business change and outbox row commit in
  the same database transaction.
- **Delivered** means an outbox handler returned success. For WebSocket messages,
  this can also mean no socket existed or a send exception was swallowed.
- **Idempotent request** means repeating it converges without additional rows or
  messages. Only the explicitly documented paths have this property.
- **At-least-once possibility** means a durable instruction may be dispatched
  again after an external success if row deletion did not commit.
