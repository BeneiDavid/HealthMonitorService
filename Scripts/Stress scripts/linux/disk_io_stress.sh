#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
TEST_RUN_ID="${1:-run_001}"
DURATION="${2:-60}"
TARGET_DIR="${3:-/tmp/thesis_stress}"
LEVEL_OR_MB="${4:-256}"
LOG_EVENT_SCRIPT="${LOG_EVENT_SCRIPT:-./log_event.sh}"
MIN_FILE_MB="${MIN_FILE_MB:-64}"
MIN_FREE_DISK_MB="${MIN_FREE_DISK_MB:-512}"

log_event() {
  "$LOG_EVENT_SCRIPT" "$EVENTS_FILE" "$TEST_RUN_ID" "$1" "${2:-}"
}

resolve_file_mb() {
  case "$1" in
    medium) echo 256 ;;
    high) echo 512 ;;
    very_high) echo 1024 ;;
    *)
      if [[ "$1" =~ ^[0-9]+$ ]] && [ "$1" -ge 1 ]; then
        echo "$1"
      else
        echo "Unsupported disk level/size: $1. Use medium, high, very_high, or a positive MB value." >&2
        exit 1
      fi
      ;;
  esac
}

mkdir -p "$TARGET_DIR"
REQUESTED_MB="$(resolve_file_mb "$LEVEL_OR_MB")"
FREE_MB="$(df -Pm "$TARGET_DIR" | awk 'NR==2 {print $4}')"
SAFE_MAX_MB=$(( FREE_MB - MIN_FREE_DISK_MB ))
[ "$SAFE_MAX_MB" -lt 0 ] && SAFE_MAX_MB=0

FILE_MB="$REQUESTED_MB"
[ "$FILE_MB" -gt "$SAFE_MAX_MB" ] && FILE_MB="$SAFE_MAX_MB"

if [ "$FILE_MB" -lt "$MIN_FILE_MB" ]; then
  log_event "stress_disk_skip" "reason=file_too_small level_or_mb=$LEVEL_OR_MB requested_mb=$REQUESTED_MB file_mb=$FILE_MB free_mb=$FREE_MB min_free_disk_mb=$MIN_FREE_DISK_MB"
  echo "Disk stress target too low for safe execution: $FILE_MB MB" >&2
  exit 1
fi

log_event "stress_disk_start" "duration=$DURATION level_or_mb=$LEVEL_OR_MB requested_mb=$REQUESTED_MB file_mb=$FILE_MB free_mb=$FREE_MB min_free_disk_mb=$MIN_FREE_DISK_MB"

end=$((SECONDS + DURATION))
i=0
while [ "$SECONDS" -lt "$end" ]; do
  file="$TARGET_DIR/file_$i.bin"
  dd if=/dev/zero of="$file" bs=1M count="$FILE_MB" conv=fsync status=none
  rm -f "$file"
  i=$((i + 1))
done

log_event "stress_disk_stop" "status=completed file_mb=$FILE_MB iterations=$i"
