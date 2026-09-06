# Deployment credentials

Before marking the internal-auth PR ready for review, the operator must add
`internal_auth_key` to the encrypted `credentials.yml` using Ansible Vault.
Use the same non-empty secret as Duely and Taski; do not commit the plaintext key
or use the local development value in production.

The playbook passes `INTERNAL_AUTH_KEY` to the coordinator and both workers.
It overrides `internal_auth_key` in `config/coordinator.yml` and
`config/worker.yml`, whose development default is `INTERNAL_AUTH_KEY_LOCAL_VALUE`.
Workers send this key on every heartbeat. Incoming request validation and
filestorage integration follow separately in issue #327.

A non-draft pull request automatically runs this repository's deployment workflow.
