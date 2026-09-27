# User registration

`POST /users/register` sends a nickname and password through MediatR to
`RegisterHandler`. Before the handler runs, the shared validation pipeline runs
all registered validators for the command. The existing `RegisterCommandValidator`
accepts Latin letters, digits, underscores, and hyphens in a nickname, and
requires a password of at least eight characters.

Validation failures return HTTP 400 with a validation problem response whose
`errors` entries identify the invalid fields. No user is saved. An existing
nickname still returns the existing conflict result. Successful registration
saves the user and returns success; the frontend then logs in.
