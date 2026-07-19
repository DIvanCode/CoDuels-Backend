# Production network topology

Production containers share the external Docker bridge network `coduels` and
address each other by container DNS name. Only Nginx publishes host ports: 80
and 443. Application APIs, dashboards, metrics, databases, and Alloy remain
reachable only inside the Docker network.

Local Compose files preserve developer access by binding internal ports to
`127.0.0.1`. These loopback bindings are development-only and must not be
copied into production playbooks.

## Connection matrix

| Caller | Destination | Port/path | Purpose |
| --- | --- | --- | --- |
| Browser/client | Nginx | host 80/443 | Public HTTP/WebSocket entrypoint |
| Nginx | Duely | `duely:5001` | Public Duely API, WebSocket, Swagger |
| Nginx | Taski | `taski:5252/task/*` | Public task metadata and files |
| Nginx | Exesh dashboard | `exesh-dashboard:9000` | Dashboard proxy |
| Duely | Taski | `taski:5252` | Start tests and poll solution messages |
| Duely | Coordinator | `coordinator:5253` | Code runs and execution messages |
| Duely | Analyzer | `analyzer:8000` | Post-duel suspicion scoring |
| Taski | Coordinator | `coordinator:5253` | Submit executions and poll messages |
| Coordinator | workers | `worker-1:5254`, `worker-2:5255` | Artifact transfer and scheduled jobs |
| Workers | Coordinator | `coordinator:5253/heartbeat` | Capacity, job requests, and results |
| Alloy | Duely | `duely:5001/metrics` | Prometheus scrape |
| Alloy | Taski | `taski:9090/` | Prometheus scrape |
| Alloy | Coordinator | `coordinator:9090/` | Prometheus scrape |
| Alloy | workers | `worker-1:9091/`, `worker-2:9092/` | Prometheus scrape |
| Alloy | Grafana Cloud | HTTPS | Metrics remote write and log shipping |

The health endpoints are `/health` on Duely, Taski, Coordinator, workers,
Analyzer, and the Exesh dashboard. Nginx exposes its own `/health`; Alloy uses
`/-/ready`. Health endpoints report process readiness and are not substitutes
for authenticated business APIs.

## Rollout verification

Run the check from a trusted shell on the target host after a deployment:

```sh
CODUELS_EXPECTED_VERSION=<root-merge-sha> ./scripts/verify-production-network.sh
```

The script verifies that every expected container is attached to `coduels`,
only Nginx publishes host ports, internal health and metrics endpoints resolve
through Docker DNS, public Nginx routes work, and the old direct host ports do
not answer on loopback. Confirm in Grafana that fresh samples arrive after at
least two scrape intervals; local readiness cannot prove remote-write receipt.

The `Production network checks` pull-request workflow validates every Compose
combination, all deployment playbooks with an empty syntax-only vars file, the
Nginx configuration, and the rollout script. It never decrypts Vault files or
contacts a production inventory.

Run the same script from an external test machine against the deployment host
or verify the firewall separately when testing the external perimeter. Docker
port inspection proves that these playbooks do not publish internal ports, but
it does not audit unrelated host firewall or process configuration.

## Rollback verification

Redeploy the last known-good root merge SHA through the approved root release
workflow or the same component playbooks used by that workflow. Do not run a
production playbook ad hoc. After rollback, rerun:

```sh
CODUELS_EXPECTED_VERSION=<last-known-good-root-sha> ./scripts/verify-production-network.sh
```

Rollback is complete only when the previous images are running, all readiness
and route checks pass, no internal host ports are published, and Grafana again
receives current samples.
