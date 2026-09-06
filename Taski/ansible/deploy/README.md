# Deployment credentials

Before marking the internal-auth PR ready for review, the operator must add
`internal_auth_key` to the encrypted `credentials.yml` using Ansible Vault.
Use the same non-empty secret as Duely and Exesh; do not commit the plaintext key
or use the local development value in production.

The playbook passes the key as `INTERNAL_AUTH_KEY`. It overrides
`internal_auth_key` in `config/taski.yml`, whose development default is
`INTERNAL_AUTH_KEY_LOCAL_VALUE`. Both execution submissions and REST history
polling send the key to Exesh. Kafka configuration is unchanged.

A non-draft pull request automatically runs this repository's deployment workflow.
