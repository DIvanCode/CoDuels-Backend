# Admin resource access

## 1. Purpose

Allow a system administrator to inspect operational and product state without
granting group membership or mutation permissions.

## 2. Authentication and authorization

The HTTP layer reads the authenticated user's `is_admin` token claim through
`IUserContext` and passes the resulting flag to read queries. A caller without
that claim continues through the existing participant, ownership, membership,
and group-role checks. Admin-only list endpoints additionally require the
`OnlyAdmin` authorization policy.

## 3. Read access

An administrator can:

- open any duel and see all task identifiers and both participants' current
  solutions;
- list both participants' submissions for any duel task and open any submission
  with its solution and testing message;
- open any group, its users, its duels, and its tournaments without being a
  member;
- open any tournament without belonging to its group.

Administrative submission-list rows include the owning `duel_id` and `task_key`
so clients can deep-link from operational lists to the corresponding submission
detail inside the duel page.

For an administrator who is not a group member, `GroupDto.user_role` is `null`.
This distinguishes system-level read access from a group role and prevents the
client from treating the administrator as an owner or manager.

## 4. Mutation boundary

The admin claim does not bypass checks for creating, updating, starting,
accepting, inviting, changing roles, excluding users, or leaving groups. Those
operations keep their existing participant, membership, and role rules.

## 5. Active-user scope

The admin active-user endpoint is intentionally process-local. Its topology and
restart semantics are documented in
[User connection lifecycle](user-connection-lifecycle.md#admin-active-user-view).

## 6. Verification

Focused handler tests cover admin and non-admin reads for duels, submissions,
groups, group users, group duels, group tournaments, and tournament details.
WebSocket service tests cover the process-local active-user boundary.
