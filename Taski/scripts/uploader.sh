#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <format> <task_path> <level>" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd
FORMAT="$1"
TASK_PATH="$2"
LEVEL="$3"

if [[ -z "$FORMAT" ]]; then
  echo "format must be non-empty" >&2
  exit 1
fi

if [[ ! -d "$TASK_PATH" && ! -f "$TASK_PATH" ]]; then
  echo "task path does not exist: $TASK_PATH" >&2
  exit 1
fi

if ! [[ "$LEVEL" =~ ^[0-9]+$ ]] || (( LEVEL < 1 || LEVEL > 10 )); then
  echo "level must be an integer in range [1..10], got: $LEVEL" >&2
  exit 1
fi

CONFIG_PATH="$SCRIPT_DIR/uploader_config.yml" \
  go run "$SCRIPT_DIR/../cmd/uploader" \
  -format "$FORMAT" \
  -src "$TASK_PATH" \
  -level "$LEVEL"
