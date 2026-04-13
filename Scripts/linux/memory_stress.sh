#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
MB="${3:-512}"

log_event() {
  echo "$(date -u +"%Y-%m-%dT%H:%M:%SZ"),$TEST_RUN_ID,$1,$2" >> "$EVENTS_FILE"
}

log_event "stress_memory_start" "duration=$DURATION memory=${MB}MB"

python3 - <<PY
import time
size_mb = int("${MB}")
duration = int("${DURATION}")

# allocate memory
data = [b"x" * 1024 * 1024 for _ in range(size_mb)]

time.sleep(duration)
print("Memory held")
PY

log_event "stress_memory_stop" "completed"