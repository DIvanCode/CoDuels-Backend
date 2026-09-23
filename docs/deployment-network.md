# Production network

Production services share the Docker bridge network named `coduels`. Containers
use Docker service names to reach each other. Host port publishing is reserved
for the public ingress; application and metrics ports remain private to the
Docker network.

## Allowed flows

| Source | Destination | Port | Purpose |
| --- | --- | ---: | --- |
| External clients | Caddy on the host | 80, 443 | HTTP redirect and HTTPS ingress |
| Caddy | Nginx | 80 | TLS-terminated requests to the reverse proxy |
| Nginx | Duely | 5001 | User, group, tournament, duel, submission, and WebSocket routes |
| Nginx | Taski | 5252 | Public task routes |
| Duely | Taski | 5252 | Testing and task requests |
| Duely | Exesh coordinator | 5253 | Code-run submission and execution status polling |
| Duely | Analyzer | 8000 | Anti-cheat prediction requests |
| Taski | Exesh coordinator | 5253 | Execution submission and status polling |
| Exesh workers | Exesh coordinator | 5253 | Authenticated heartbeats and job dispatch |
| Exesh workers | Exesh workers | 5254, 5255 | Transfer execution artifacts between workers |
| Alloy | Duely | 5001 | Scrape Duely `/metrics` |
| Alloy | Taski | 9090 | Scrape Taski metrics |
| Alloy | Exesh coordinator | 9090 | Scrape coordinator metrics |
| Alloy | Exesh workers | 9091, 9092 | Scrape worker metrics |

Duely, Taski, and Exesh also connect to PostgreSQL using their configured
connection strings. Those database endpoints are managed outside these
service containers.

## Host exposure

Only Caddy publishes host ports `80` and `443`. Nginx, application APIs,
metrics listeners, and Alloy's HTTP server are reachable by containers on
`coduels` and are not published on the host. Alloy discovers and scrapes the
service DNS names above, so monitoring does not depend on host port mappings.

The production Ansible playbooks wait for application listeners and metrics
readiness over each container's `coduels` address. Container inspection tasks
are hidden from Ansible output because Docker inspection includes environment
variables.

Development Compose files bind developer-accessible ports to `127.0.0.1`.
Production topology is defined by the Ansible deployment playbooks, which do
not publish application ports.

The `coduels` bridge provides connectivity and DNS, but it does not isolate
containers from each other. Internal endpoints must continue to enforce the
shared `internal-auth` key where required.
