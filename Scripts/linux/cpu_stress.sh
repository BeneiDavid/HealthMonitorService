#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
WORKERS="${3:-2}"

PIDS=()

log_event() {
  echo "$(date -u +"%Y-%m-%dT%H:%M:%SZ"),$TEST_RUN_ID,$1,$2" >> "$EVENTS_FILE"
}

cleanup() {
  for pid in "${PIDS[@]:-}"; do
    kill "$pid" 2>/dev/null || true
  done

  for pid in "${PIDS[@]:-}"; do
    wait "$pid" 2>/dev/null || true
  done
}

trap cleanup EXIT INT TERM

log_event "stress_cpu_start" "duration=$DURATION workers=$WORKERS"

for i in $(seq 1 "$WORKERS"); do
  yes > /dev/null &
  PIDS+=("$!")
done

sleep "$DURATION"

cleanup
trap - EXIT INT TERM

log_event "stress_cpu_stop" "completed"