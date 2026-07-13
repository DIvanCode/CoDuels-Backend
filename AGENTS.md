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
- For EF model changes, add/update migrations and verify migration startup through the Duely migration image.
- For Taski/Exesh contract changes, update both sides and any Duely gateway or polling consumer.
- Preserve `isolate` for untrusted runtime jobs and preserve worker resource accounting.
- Use `gofmt` on changed Go files. Do not introduce a new formatter or dependency without a concrete need.
- Do not decrypt vault files or run deploy playbooks without explicit user authorization.
- This repository has no production deployment workflow. After a validated backend change is merged here, release it by advancing the `Backend` submodule in a pull request to root `CoDuels`.

## Verification

- Duely: `cd Duely && dotnet test --configuration Release`.
- Exesh: `cd Exesh && go test ./...`.
- Taski: `cd Taski && go test ./...`.
- filestorage: `cd filestorage && go test ./...`.
- Analyzer has no committed automated test suite; at minimum syntax-check changed Python and exercise feature/model code relevant to the change. Train both models with `cd Analyzer && python train.py --data-dir data/train` when the feature schema or training code changes.
- Use the matching GitHub Actions workflow as the final CI command reference.
