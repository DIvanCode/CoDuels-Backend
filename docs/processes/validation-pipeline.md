# MediatR validation audit

`SetupUseCases` registers FluentValidation validators from the use-case assembly
and runs matching `IValidator<TRequest>` instances before each MediatR handler.
HTTP maps `ValidationException` to a field-based 400 response. The WebSocket
transport logs validation failures for `SolutionUpdated` and continues reading;
HTTP problem details do not apply after a WebSocket upgrade.

The following inventory was checked against its actual producer and handler:

| Validator | Producer and compatibility check |
| --- | --- |
| `RegisterCommandValidator` | Registration form and `POST /users/register`: existing nickname rule and eight-character password minimum remain unchanged. |
| `GetUserByTicketCommandValidator` | `GET /users/connect`: generated tickets are nonempty; an empty ticket is an invalid handshake. |
| `GetUserActionsQueryValidator` | `GET /actions`: duel/user ids and task key identify an action stream; the client reads concrete streams. |
| `SaveUserActionsCommandValidator` | `POST /actions`: frontend queue sends nonempty batches. A null action is invalid. |
| `UserActionValidator` | Runs for each saved action through `SaveUserActionsCommandValidator`: the queue generates nonempty event ids, positive sequence ids, timestamps, and duel ids; action `task_key` is required by the HTTP model too. |
| `CreateCodeRunCommandValidator` | `POST /code-runs`: the UI rejects blank code; task limits/key/language come from the selected task; input above 10,000 characters is explained before sending. |
| `UpdateDuelTaskSolutionCommandValidator` | WebSocket `SolutionUpdated`: empty solution is valid when the editor is cleared; null solution and invalid ids/key/language are rejected without closing the socket. |
| `CreateDuelConfigurationCommandValidator` | Configuration form sends sequential task keys and server-advertised levels; duration and task-count controls match the 5–300 minute and 1–10 task bounds. Null task data produces validation errors. |
| `UpdateDuelConfigurationCommandValidator` | Same form and bounds as creation; null task data produces validation errors. |
| `CreateGroupCommandValidator` | Group creation trims and requires a nonempty name before sending. |
| `UpdateGroupCommandValidator` | Group editing sends a nonempty name. |
| `CreateTournamentCommandValidator` | Tournament form requires two or more selected members, nonempty name, and a supported matchmaking type; null participant data produces a validation error. |

The frontend action type still permits an omitted `task_key`, while the HTTP
model requires it. Normal editor tracking has a selected task key; if that key
is absent, the HTTP model already rejects the batch before MediatR. This is a
pre-existing contract mismatch, not a new effect of the validation pipeline.
