#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
TARGET_DIR="${3:-/tmp/thesis_stress}"
FILE_MB="${4:-256}"

log_event() {
  echo "$(date -u +"%Y-%m-%dT%H:%M:%SZ"),$TEST_RUN_ID,$1,$2" >> "$EVENTS_FILE"
}

mkdir -p "$TARGET_DIR"

log_event "stress_disk_start" "duration=$DURATION file=${FILE_MB}MB"

end=$((SECONDS + DURATION))
i=0

while [ "$SECONDS" -lt "$end" ]; do
  file="$TARGET_DIR/file_$i.bin"

  dd if=/dev/zero of="$file" bs=1M count="$FILE_MB" conv=fsync status=none
  rm -f "$file"

  i=$((i + 1))
done

log_event "stress_disk_stop" "completed"