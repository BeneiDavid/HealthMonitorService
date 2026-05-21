#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
WORKERS="${3:-2}"
LOG_EVENT_SCRIPT="${LOG_EVENT_SCRIPT:-./log_event.sh}"
CPU_CORES="$(nproc)"
PIDS=()

log_event() {
  "$LOG_EVENT_SCRIPT" "$EVENTS_FILE" "$TEST_RUN_ID" "$1" "${2:-}"
}

if ! [[ "$WORKERS" =~ ^[0-9]+$ ]] || [ "$WORKERS" -lt 1 ]; then
  echo "CPU workers must be a positive integer" >&2
  exit 1
fi

if [ "$WORKERS" -gt "$CPU_CORES" ]; then
  WORKERS="$CPU_CORES"
fi

cleanup() {
  for pid in "${PIDS[@]:-}"; do
    kill "$pid" 2>/dev/null || true
  done

  for pid in "${PIDS[@]:-}"; do
    wait "$pid" 2>/dev/null || true
  done
}
trap cleanup EXIT INT TERM

log_event "stress_cpu_start" "duration=$DURATION workers=$WORKERS total_cores=$CPU_CORES"

for _ in $(seq 1 "$WORKERS"); do
  yes > /dev/null &
  PIDS+=("$!")
done

sleep "$DURATION"

cleanup
trap - EXIT INT TERM
log_event "stress_cpu_stop" "status=completed"
