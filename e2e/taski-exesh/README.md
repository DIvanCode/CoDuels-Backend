# Taski → Exesh A+B end-to-end test

The test starts an isolated Taski/Exesh stack, submits a correct C++ A+B
solution with a random solution ID, and polls Taski's REST API until the last
stored message is `finish` with the `Accepted` verdict.

Run it from the Backend repository:

```sh
./e2e/taski-exesh/run.sh
```

The Compose project uses no host ports or fixed container names, so it can run
alongside the regular local CoDuels stack. Containers, networks, and volumes
created by the test are removed when it finishes.

The `Taski-Exesh end-to-end` pull-request workflow runs this command when a PR
changes Taski, Exesh, this scenario, or related root Compose/submodule
configuration. Keep the scenario's Compose services, test configs, seeded task,
requests, and assertions synchronized with relevant Taski/Exesh contracts and
infrastructure. General conventions for maintaining and adding scenarios are
in [`../README.md`](../README.md).
