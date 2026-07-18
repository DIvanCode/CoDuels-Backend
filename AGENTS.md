# Backend agent guide

## Repository shape

- This is the `CoDuels-Backend` Git repository. It contains Duely, Analyzer, Taski, Exesh, Nginx, Alloy, and four submodules listed in `.gitmodules`.
- Treat `filestorage` and `Taski/tasks` as separate project repositories. Treat `Exesh/isolate` and `Exesh/testlib` as upstream code; change them only when explicitly required.
- The current production event path is not identical to the thesis: Taski and Exesh dispatch/status polling are configured for REST in Ansible and Duely production settings. Confirm runtime modes before changing Kafka or polling code.

## Component ownership

- Duely owns product/domain behavior and public browser-facing HTTP/WebSocket contracts.
- Taski owns tasks, task packages, testing strategies, execution graph construction, and verdicts.
- Exesh owns execution persistence, scheduling, worker capacity, job execution, artifact flow, and isolation.
- Analyzer owns action schemas, feature extraction, model training, and `/predict`.
- filestorage owns bucket lifecycle and transfers; avoid duplicating file-transfer code in Taski or Exesh.

## Change rules

- Keep handlers/controllers thin. Put orchestration in application/usecase layers and invariants in domain layers.
- Use Conventional Commits (`type(scope): description`) for every commit and use the same concise, imperative convention for pull-request titles.
- For EF model changes, add/update migrations and verify migration startup through the Duely migration image.
- For Taski/Exesh contract changes, update both sides and any Duely gateway or polling consumer.
- Preserve `isolate` for untrusted runtime jobs and preserve worker resource accounting.
- Use `gofmt` on changed Go files. Do not introduce a new formatter or dependency without a concrete need.
- For every Taski or Exesh change, run `./e2e/taski-exesh/run.sh` after focused tests. If Docker or the required isolation features are unavailable, report the test as not run and give the exact reason.
- When Taski, Exesh, their Docker/Compose/Ansible configuration, task fixtures, submodules, or related infrastructure changes, review the Taski-Exesh e2e scenario and update its services, configs, fixtures, requests, or assertions when the contract changed.
- New cross-service e2e scenarios must follow `e2e/README.md`, have one local `run.sh`, and add a non-duplicating pull-request workflow whose paths cover every participating service plus related configuration and infrastructure.
- Do not decrypt vault files or run deploy playbooks without explicit user authorization.
- This repository has no production deployment workflow. After a validated backend change is merged here, release it by advancing the `Backend` submodule in a pull request to root `CoDuels`.

## Business process documentation

- Before changing business logic, read the corresponding document in `docs/processes`.
- Update the process documentation whenever a change affects state transitions, cancellation conditions, permissions, messages, recipients, side effects, retries, timeouts, or WebSocket behavior.
- Treat messages that inform clients about state transitions as part of application behavior.
- Do not add or remove messages, change their payloads, or change their recipients without updating both the documentation and the relevant tests.
- Analyze cleanup of conflicting pending states in addition to the primary state change.
- Do not replace specialized cleanup with a general handler until its effect on every pending-duel type has been checked.
- If code and documentation disagree, report the discrepancy and do not silently change behavior.
- Do not automatically treat the current implementation as the correct product requirement.
- Keep proposed behavior separate from documented current behavior.
- When a process changes, update or add tests for state transitions, canceled states, emitted messages, recipients, idempotency, and negative scenarios.

## Verification

- Duely: `cd Duely && dotnet test --configuration Release`.
- Exesh: `cd Exesh && go test ./...`.
- Taski: `cd Taski && go test ./...`.
- Taski-Exesh e2e after any Taski or Exesh change: `./e2e/taski-exesh/run.sh` from the Backend repository.
- filestorage: `cd filestorage && go test ./...`.
- Analyzer has no committed automated test suite; at minimum syntax-check changed Python and exercise feature/model code relevant to the change. Train both models with `cd Analyzer && python train.py --data-dir data/train` when the feature schema or training code changes.
- Use the matching GitHub Actions workflow as the final CI command reference.

## Distributed execution process documentation

- Before changing an Exesh scheduler, worker, heartbeat, artifact flow, persistence, or dispatcher, read the corresponding documents in `docs/processes/exesh`.
- Analyze persisted and in-memory state separately.
- A scheduler change must account for priority, promises, slots, memory, retries, worker loss, and restart.
- A heartbeat change must account for result redelivery, partial processing, idempotency, resource accounting, and worker re-registration.
- A job-completion change must account for graph transitions, category statistics, messages, artifacts, persistence, and unlocking dependent jobs.
- An artifact-flow change must account for location ownership, TTL, worker failure, downstream download, and the absence of replicas.
- Execution messages are part of Exesh's external contract.
- Do not change message type, order, payload, or emission conditions without updating documentation and tests.
- Do not claim exactly-once delivery without demonstrated guarantees.
- Analyze every retry for duplicate execution, job, artifact, and message effects.
- Do not replace process-local state with persisted state, or persisted state with process-local state, without documenting restart semantics.
- If code and documentation disagree, report the discrepancy explicitly.
- Do not automatically treat current behavior as the correct requirement.
- Keep proposed changes separate from documented current behavior.
- When a distributed process changes, add or update tests for state transitions, idempotency, duplicate delivery, worker loss, coordinator restart, heartbeat failure, artifact loss, resource accounting, and concurrency.

## Task testing process documentation

- Before changing Taski task storage, a testing strategy, the Exesh contract, an event consumer, verdict logic, or the dispatcher, read the corresponding document in `docs/processes/taski`.
- A testing-strategy change must account for stages, dependencies, job names, success statuses, sources, artifacts, intermediate statuses, and the final verdict.
- Treat stage and job names as possible persisted-state and event-processing contracts.
- Do not change job names without analyzing old unfinished Solutions.
- Do not change serialized strategy data without analyzing backward compatibility.
- An Exesh-call change must account for the absence of a shared transaction between Taski PostgreSQL and Exesh.
- Analyze every retry that starts testing for duplicate Exesh executions.
- Analyze Kafka and REST polling modes separately.
- Event-handling changes must cover ordering, duplicates, missing and early events, events after finish, and unknown executions.
- Do not change `HandledEventsCount` or a message cursor without analyzing restart semantics.
- Treat Taski messages as part of the external Duely contract.
- Do not change a message type, payload, order, or emission condition without updating documentation and tests.
- A message-history change must account for polling consumers.
- An outbox change must account for duplicate Kafka delivery.
- Do not claim exactly-once delivery without demonstrated guarantees.
- A task-bucket-format change must account for existing tasks.
- A Polygon-uploader change must account for re-upload, bucket rollback, and archive safety.
- If code and documentation disagree, report the discrepancy explicitly.
- Do not automatically treat current behavior as the correct requirement.
- Keep proposed changes separate from documented current behavior.
- When a Taski process changes, add or update tests for state transitions, graph construction, verdict calculation, duplicate and out-of-order events, transaction rollback, Exesh and Kafka failures, REST polling recovery, outbox retry, and backward compatibility.
