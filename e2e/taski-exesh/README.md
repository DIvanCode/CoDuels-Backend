# Taski → Exesh A+B end-to-end test

The test starts an isolated Taski/Exesh stack, submits a correct C++ A+B
solution with a random solution ID, and polls Taski's REST API until the last
stored message is `finish` with the `Accepted` verdict.

Coordinator and worker intentionally retain relative filestorage roots to
exercise the path shape that caused the production failure. Production now
uses the equivalent canonical absolute paths as an additional safeguard, but
the test still prevents relative-versus-absolute regressions in filestorage.

The e2e Exesh image replaces the released filestorage module with the checked
out `filestorage` submodule. A backend pull request that advances filestorage
therefore tests that exact revision before a new module version is published.

Run it from the Backend repository:

```sh
./e2e/taski-exesh/run.sh
```

The Compose project uses no host ports or fixed container names, so it can run
alongside the regular local CoDuels stack. Containers, networks, and volumes
created by the test are removed when it finishes. Taski, Coordinator, and the
worker must pass their container health checks before the test client starts.

The `End-to-end tests` pull-request workflow in
`.github/workflows/e2e_tests_pull_request.yml` runs this command once when a PR
changes Taski or Exesh. The workflow is independent of the component workflows:
Taski and Exesh builds and deployments do not wait for this job. Changes only
to this scenario or related root Compose/submodule configuration do not trigger
the current workflow. Keep the scenario's Compose services, test configs,
seeded task, requests, and assertions synchronized with relevant Taski/Exesh
contracts and infrastructure. General conventions for maintaining and adding
scenarios are in [`../README.md`](../README.md).
