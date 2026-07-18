#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
project_name="coduels-taski-exesh-e2e-$$"

cleanup() {
  docker compose \
    --project-name "$project_name" \
    --file "$script_dir/compose.yml" \
    down --volumes --remove-orphans
}

trap cleanup EXIT INT TERM

docker compose \
  --project-name "$project_name" \
  --file "$script_dir/compose.yml" \
  up --build --abort-on-container-exit --exit-code-from test
