# User registration

`POST /users/register` sends a nickname and password through MediatR to
`RegisterHandler`. Before the handler runs, the shared validation pipeline runs
all registered validators for the command. The existing `RegisterCommandValidator`
accepts Latin letters, digits, underscores, and hyphens in a nickname, and
requires a password of at least eight characters.

Validation failures return HTTP 400 with a validation problem response whose
`errors` entries identify the invalid fields. No user is saved. An existing
nickname, including one that differs only by letter case, returns the existing
conflict result. `Nickname` keeps the registered spelling for display. The
database stores a generated lowercase `NicknameLower` column with a unique
index, so simultaneous registrations cannot create case variants. Existing
users with colliding nicknames must be reconciled before the migration can
complete. Successful registration saves the user and returns success; the
frontend then logs in. Login and nickname-based user lookup use the lowercase
column and return the user's registered spelling.
