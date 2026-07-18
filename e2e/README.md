# Backend end-to-end test conventions

End-to-end tests in this directory validate contracts that cross service and
infrastructure boundaries. They complement, rather than replace, the unit and
component tests owned by each service.

## Required test shape

Each scenario lives in `e2e/<scenario>/` and provides:

- one documented `run.sh` entry point used both locally and in CI;
- an isolated Compose project without fixed container names or host ports;
- explicit test-only service configs and deterministic fixtures;
- bounded readiness polling and an overall timeout;
- assertions for the externally observable terminal result, including its
  stability when late messages are relevant;
- unconditional cleanup of containers, networks, and volumes.

Untrusted solutions must run only through an Exesh worker with `isolate`; an
end-to-end fixture must never execute submitted code directly on the host.

## Keeping a scenario current

When changing a service covered by an end-to-end scenario, run its local entry
point after focused tests. Review and update the scenario in the same change
when any of these can affect it:

- HTTP, message, job, artifact, task-package, or persisted-state contracts;
- configuration keys, defaults, runtime modes, readiness, or timeouts;
- Dockerfiles, Compose wiring, Ansible service configuration, submodules, or
  other infrastructure used by the participating services;
- seeded data, task packages, supported languages, or expected verdicts.

A code edit does not require a cosmetic end-to-end edit when the scenario is
still correct, but its test must still be run and reported.

## Adding a scenario

Add a dedicated pull-request workflow that:

1. runs for every participating service, the scenario directory, the workflow
   itself, and related config or infrastructure paths outside those services;
2. checks out all required submodules recursively;
3. grants only `contents: read`, sets a bounded job timeout, and invokes the
   same `run.sh` used by developers;
4. avoids duplicate runs when one pull request changes several participating
   services;
5. documents the trigger paths and local command in the scenario README and in
   the repository's agent verification guidance.
