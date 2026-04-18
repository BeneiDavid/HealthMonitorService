#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
LEVEL_OR_PERCENT="${3:-high}"

TOTAL_MEM_MB="$(awk '/MemTotal:/ {printf "%d\n", $2/1024}' /proc/meminfo)"
AVAILABLE_MB="$(awk '/MemAvailable:/ {printf "%d\n", $2/1024}' /proc/meminfo)"
DEFAULT_MIN_FREE_MB=$(( TOTAL_MEM_MB * 5 / 100 ))
[ "$DEFAULT_MIN_FREE_MB" -lt 200 ] && DEFAULT_MIN_FREE_MB=200
MIN_FREE_MB="${MIN_FREE_MB:-$DEFAULT_MIN_FREE_MB}"
MIN_TARGET_MB="${MIN_TARGET_MB:-256}"
OOM_SCORE_ADJ_VALUE="${OOM_SCORE_ADJ_VALUE:--500}"
CHUNK_MB="${CHUNK_MB:-256}"
SLEEP_BETWEEN_CHUNKS="${SLEEP_BETWEEN_CHUNKS:-0.05}"
KEEP_TOUCHING_INTERVAL="${KEEP_TOUCHING_INTERVAL:-1.0}"
LOG_EVENT_SCRIPT="${LOG_EVENT_SCRIPT:-./log_event.sh}"

log_event() {
  "$LOG_EVENT_SCRIPT" "$EVENTS_FILE" "$TEST_RUN_ID" "$1" "${2:-}"
}

resolve_percent() {
  case "$1" in
    medium) echo 65; return 0;;
    high) echo 85; return 0;;
    very_high) echo 100; return 0;;
    *%)
      local value="${1%%%}"
      [[ "$value" =~ ^[0-9]+$ ]] && [ "$value" -ge 1 ] && [ "$value" -le 100 ] && echo "$value" && return
      ;;
    *)
      [[ "$1" =~ ^[0-9]+$ ]] && [ "$1" -ge 1 ] && [ "$1" -le 100 ] && echo "$1" && return
      ;;
  esac

  echo "Unsupported memory level/percent: $1. Use medium, high, very_high, or 1-100 / 1-100%." >&2
  exit 1
}

cleanup() {
  if [ -n "${PY_PID:-}" ]; then
    kill "$PY_PID" 2>/dev/null || true
    wait "$PY_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

PERCENT="$(resolve_percent "$LEVEL_OR_PERCENT")"

REQUESTED_MB=$(( AVAILABLE_MB * PERCENT / 100 ))
SAFE_CEILING_MB=$(( AVAILABLE_MB - MIN_FREE_MB ))
[ "$SAFE_CEILING_MB" -lt 0 ] && SAFE_CEILING_MB=0

TARGET_MB="$REQUESTED_MB"
[ "$TARGET_MB" -gt "$SAFE_CEILING_MB" ] && TARGET_MB="$SAFE_CEILING_MB"

if [ "$TARGET_MB" -lt "$MIN_TARGET_MB" ]; then
  log_event "stress_memory_skip" "reason=target_too_low level_or_percent=$LEVEL_OR_PERCENT percent=$PERCENT requested_mb=$REQUESTED_MB target_mb=$TARGET_MB available_mb=$AVAILABLE_MB min_free_mb=$MIN_FREE_MB safe_ceiling_mb=$SAFE_CEILING_MB"
  echo "Memory stress target too low for safe execution: $TARGET_MB MB" >&2
  exit 1
fi

if [ -w "/proc/$$/oom_score_adj" ]; then
  if [ "$OOM_SCORE_ADJ_VALUE" -ge 0 ] || [ "$(id -u)" -eq 0 ]; then
    printf '%s' "$OOM_SCORE_ADJ_VALUE" > "/proc/$$/oom_score_adj" 2>/dev/null || true
  fi
fi

log_event "stress_memory_start" "duration=$DURATION level_or_percent=$LEVEL_OR_PERCENT percent=$PERCENT requested_mb=$REQUESTED_MB target_mb=$TARGET_MB total_mem_mb=$TOTAL_MEM_MB available_mb=$AVAILABLE_MB min_free_mb=$MIN_FREE_MB safe_ceiling_mb=$SAFE_CEILING_MB oom_score_adj=$OOM_SCORE_ADJ_VALUE"

python3 - "$TARGET_MB" "$DURATION" "$CHUNK_MB" "$SLEEP_BETWEEN_CHUNKS" "$KEEP_TOUCHING_INTERVAL" <<'PY' &
import sys
import time

target_mb = int(sys.argv[1])
duration = int(sys.argv[2])
chunk_mb = int(sys.argv[3])
sleep_between = float(sys.argv[4])
keep_touching_interval = float(sys.argv[5])

chunks = []
allocated_mb = 0
page_size = 4096

end_time = time.time() + duration

while allocated_mb < target_mb:
    current_mb = min(chunk_mb, target_mb - allocated_mb)
    block = bytearray(current_mb * 1024 * 1024)
    for i in range(0, len(block), page_size):
        block[i] = 1
    chunks.append(block)
    allocated_mb += current_mb
    time.sleep(sleep_between)

while time.time() < end_time:
    for block in chunks:
        if block:
            block[0] = (block[0] + 1) % 256
    time.sleep(keep_touching_interval)
PY

PY_PID=$!
if ! wait "$PY_PID"; then
  log_event "stress_memory_stop" "status=failed target_mb=$TARGET_MB"
  exit 1
fi

trap - EXIT INT TERM
log_event "stress_memory_stop" "status=completed target_mb=$TARGET_MB"
