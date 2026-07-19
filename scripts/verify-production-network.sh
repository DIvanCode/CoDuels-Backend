#!/usr/bin/env bash
set -euo pipefail

network="${CODUELS_NETWORK:-coduels}"
probe_image="${CODUELS_PROBE_IMAGE:-curlimages/curl:8.12.1}"
public_url="${CODUELS_PUBLIC_URL:-http://127.0.0.1}"
expected_version="${CODUELS_EXPECTED_VERSION:-}"

private_containers=(
  duely
  taski
  coordinator
  worker-1
  worker-2
  analyzer
  exesh-dashboard
  alloy
)
all_containers=("${private_containers[@]}" nginx)

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

command -v docker >/dev/null || fail "docker is required"
command -v curl >/dev/null || fail "curl is required"
docker network inspect "$network" >/dev/null 2>&1 || fail "Docker network '$network' does not exist"

for container in "${all_containers[@]}"; do
  docker inspect "$container" >/dev/null 2>&1 || fail "container '$container' is missing"
  container_ip="$(docker inspect --format "{{with index .NetworkSettings.Networks \"$network\"}}{{.IPAddress}}{{end}}" "$container")"
  [[ -n "$container_ip" ]] || fail "container '$container' is not attached to '$network'"
done

for container in "${private_containers[@]}"; do
  published="$(docker inspect --format '{{range $port, $bindings := .NetworkSettings.Ports}}{{if $bindings}}{{$port}} {{end}}{{end}}' "$container")"
  [[ -z "$published" ]] || fail "container '$container' publishes internal ports: $published"
done

nginx_ports="$(docker inspect --format '{{range $port, $bindings := .NetworkSettings.Ports}}{{if $bindings}}{{$port}}{{"\n"}}{{end}}{{end}}' nginx | sort | xargs)"
[[ "$nginx_ports" == "443/tcp 80/tcp" ]] || fail "nginx publishes unexpected ports: ${nginx_ports:-none}"

if [[ -n "$expected_version" ]]; then
  for container in duely taski coordinator worker-1 worker-2 analyzer exesh-dashboard; do
    image="$(docker inspect --format '{{.Config.Image}}' "$container")"
    [[ "$image" == *":$expected_version" ]] || fail "container '$container' uses '$image', expected tag '$expected_version'"
  done
fi

probe() {
  local url="$1"
  docker run --rm --network "$network" "$probe_image" \
    --fail --silent --show-error --max-time 5 "$url" >/dev/null
}

probe http://duely:5001/health
probe http://taski:5252/health
probe http://coordinator:5253/health
probe http://worker-1:5254/health
probe http://worker-2:5255/health
probe http://analyzer:8000/health
probe http://exesh-dashboard:9000/health
probe http://alloy:12345/-/ready
probe http://duely:5001/metrics
probe http://taski:9090/
probe http://coordinator:9090/
probe http://worker-1:9091/
probe http://worker-2:9092/

public_url="${public_url%/}"
curl --fail --silent --show-error --max-time 5 "$public_url/health" >/dev/null
curl --fail --silent --show-error --max-time 5 "$public_url/api/swagger/index.html" >/dev/null
curl --fail --silent --show-error --max-time 5 "$public_url/api/task/topics" >/dev/null
curl --fail --silent --show-error --max-time 5 "$public_url/exesh-dashboard/health" >/dev/null

for port in 5001 5252 5253 5254 5255 8000 9000 9077 9080 9081 9082 12345; do
  if curl --silent --output /dev/null --connect-timeout 1 --max-time 1 "http://127.0.0.1:$port/"; then
    fail "internal host port '$port' accepts HTTP connections"
  fi
done

echo "Production network verification passed."
