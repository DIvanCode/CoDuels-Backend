# User registration

`POST /users/register` sends a nickname and password to `RegisterHandler`.
Before checking for an existing nickname or saving a user, the handler validates
that the nickname is nonempty and contains only ASCII Latin letters (`a-z`,
`A-Z`), digits (`0-9`), and underscores (`_`). Hyphens, spaces, non-Latin
letters, and punctuation are rejected.

Nickname validation failures return HTTP 400 with a validation problem response
whose `errors.Nickname` entry describes the character rule. No user is saved.
An existing nickname still returns the existing conflict result. Successful
registration saves the user and returns success; the frontend then logs in.
