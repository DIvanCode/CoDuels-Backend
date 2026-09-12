# Dashboard retirement

The diploma dashboard is retired in issue #329. Exesh no longer builds or
publishes its Django image, starts its container through Ansible or local
Compose, runs its Python check in CI, or records data for its charts. The Nginx
configuration already has no dashboard route; its CI check no longer adds a
placeholder dashboard host.

## Removed data and collection

- `exesh_execution_events`, `exesh_job_events`, and `exesh_worker_events`, including
  their indexes, asynchronous write queue, and seven-day retention worker.
- Candidate priority/progress snapshots, execution duration/status snapshots,
  promise latency, job start/finish snapshots, and worker capacity snapshots.
- Memory offsets and the free-interval search used to position chart bars.
- The dashboard-only execution ID and job timing/worker fields in scheduler
  bookkeeping. Running-job start times remain in the worker pool because promise
  scheduling uses them.

## Preserved execution behavior

`category_time_histogram` and `category_memory_histogram` retain their data and
continue receiving recognized result samples. They supply the median/maximum
estimates used for execution weight, priority, and worker placement; they were
not exclusive to the dashboard. Expected progress, priorities, slots, memory
reservations, promise rescheduling, artifact ownership, worker expiry, retry and
restart semantics are unchanged.

Public execution/job messages and their REST history/Kafka outbox contracts are
unchanged. Prometheus `coduels_exesh_coordinator_now_weight`, Go/process metrics,
and application logs remain available.

## Upgrade behavior

Coordinator storage initialization calls `postgres.Migrate` inside its normal
initialization transaction, before creating the retained tables. It executes
`DROP TABLE IF EXISTS` for the three legacy event tables without `CASCADE`.
Existing chart history is permanently discarded. Fresh installations and later
restarts safely repeat the cleanup. Retained table contents are unaffected.
An unexpected dependent object or insufficient database ownership fails storage
initialization and rolls back the transaction; the coordinator does not schedule
work after failed initialization. The initialization timeout also bounds waiting
for a conflicting lock.

The updated Ansible playbook no longer manages the old `dashboard` container.
The operator must remove that production container manually, as requested in the
issue, before restarting the updated coordinator so dashboard queries do not
hold locks on the retired tables. No container deletion or production deployment
is performed by this change. The encrypted deployment credentials file remains
unchanged; its unused dashboard connection value is no longer referenced.

Rolling back to an older coordinator recreates empty event tables; it cannot
restore discarded chart history.

## Verification

- Scheduler regression tests cover resource release before callbacks, concurrent
  result redelivery, unknown results, promised job dispatch, source failure, and
  replacement/repeated removal accounting.
- `TestMigrateRetiresOnlyDashboardTables` runs against PostgreSQL when
  `EXESH_TEST_POSTGRES_DSN` is set. It checks populated legacy tables, retained
  rows, repeat cleanup, dependency refusal, and transactional rollback in an
  isolated schema. The Exesh PR test job supplies an isolated PostgreSQL service.
- The existing Taski-Exesh e2e remains the acceptance check for public messages,
  task execution and isolation. Its Compose/config/fixture contracts are
  unaffected by dashboard removal.
