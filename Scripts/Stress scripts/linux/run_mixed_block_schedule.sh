#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
SCHEDULE_FILE="${1:?Usage: ./run_mixed_block_schedule.sh <mixed_schedule.csv> [run_id] [cooldown_seconds]}"
RUN_ID="${2:-run_001}"
COOLDOWN_SECONDS="${3:-15}"

CPU_SCRIPT="${CPU_SCRIPT:-./cpu_stress.sh}"
MEM_SCRIPT="${MEM_SCRIPT:-./memory_stress.sh}"
DISK_SCRIPT="${DISK_SCRIPT:-./disk_io_stress.sh}"
LOG_EVENT_SCRIPT="${LOG_EVENT_SCRIPT:-./log_event.sh}"
DISK_TARGET_DIR="${DISK_TARGET_DIR:-/tmp/thesis_stress}"

CPU_CORES="$(nproc)"
CPU_MEDIUM=$(( CPU_CORES / 2 ))
(( CPU_MEDIUM < 1 )) && CPU_MEDIUM=1
CPU_HIGH=$(( CPU_CORES - 1 ))
(( CPU_HIGH < 1 )) && CPU_HIGH=1
CPU_VERY_HIGH="$CPU_CORES"

mkdir -p "$DISK_TARGET_DIR"

log_event() {
  "$LOG_EVENT_SCRIPT" "$EVENTS_FILE" "$RUN_ID" "$1" "${2:-}"
}

validate_level_or_none() {
  case "$2" in
    none|medium|high|very_high) ;;
    *)
      echo "Unknown $1 level: $2. Use none, medium, high, or very_high." >&2
      exit 1
      ;;
  esac
}

resolve_cpu_workers() {
  case "$1" in
    medium) echo "$CPU_MEDIUM" ;;
    high) echo "$CPU_HIGH" ;;
    very_high) echo "$CPU_VERY_HIGH" ;;
    none) echo "" ;;
    *)
      echo "Unknown CPU level: $1. Use none, medium, high, or very_high." >&2
      exit 1
      ;;
  esac
}

CURRENT_PIDS=()

cleanup_children() {
  for pid in "${CURRENT_PIDS[@]:-}"; do
    kill "$pid" 2>/dev/null || true
  done
  for pid in "${CURRENT_PIDS[@]:-}"; do
    wait "$pid" 2>/dev/null || true
  done
}
trap cleanup_children INT TERM EXIT

run_mixed() {
  local cpu_level="$1"
  local mem_level="$2"
  local disk_level="$3"
  local duration="$4"

  validate_level_or_none "cpu" "$cpu_level"
  validate_level_or_none "memory" "$mem_level"
  validate_level_or_none "disk" "$disk_level"

  CURRENT_PIDS=()
  local details="duration=${duration}s"
  local started_other=0
  local mem_duration="$duration"
  local workers=""

  if [ "$cpu_level" != "none" ]; then
    workers="$(resolve_cpu_workers "$cpu_level")"
    details="$details cpu=$cpu_level workers=$workers total_cores=$CPU_CORES"
  fi

  if [ "$disk_level" != "none" ]; then
    details="$details disk=$disk_level target_dir=$DISK_TARGET_DIR"
  fi

  if [ "$mem_level" != "none" ]; then
    if [ "$cpu_level" != "none" ] || [ "$disk_level" != "none" ]; then
      if [ "$duration" -le 5 ]; then
        echo "Duration must be greater than 5 seconds when memory is delayed after CPU/disk." >&2
        exit 1
      fi
      mem_duration=$(( duration - 5 ))
    fi

    details="$details memory=$mem_level memory_duration=${mem_duration}s"
  fi

  if [ "$cpu_level" = "none" ] && [ "$mem_level" = "none" ] && [ "$disk_level" = "none" ]; then
    echo "Mixed scenario must enable at least one resource" >&2
    exit 1
  fi

  log_event "scenario_start" "$details"

  if [ "$cpu_level" != "none" ]; then
    "$CPU_SCRIPT" "$RUN_ID" "$duration" "$workers" &
    CURRENT_PIDS+=("$!")
    started_other=1
  fi

  if [ "$disk_level" != "none" ]; then
    "$DISK_SCRIPT" "$RUN_ID" "$duration" "$DISK_TARGET_DIR" "$disk_level" &
    CURRENT_PIDS+=("$!")
    started_other=1
  fi

  if [ "$mem_level" != "none" ]; then
    if [ "$started_other" -eq 1 ]; then
      sleep 5
    fi

    "$MEM_SCRIPT" "$RUN_ID" "$mem_duration" "$mem_level" &
    CURRENT_PIDS+=("$!")
  fi

  local failed=0
  for pid in "${CURRENT_PIDS[@]}"; do
    if ! wait "$pid"; then
      failed=1
    fi
  done
  CURRENT_PIDS=()

  if [ "$failed" -ne 0 ]; then
    log_event "scenario_stop" "status=failed duration=${duration}s cpu=$cpu_level memory=$mem_level disk=$disk_level"
    echo "One or more mixed stress processes failed" >&2
    exit 1
  fi

  log_event "scenario_stop" "status=completed duration=${duration}s cpu=$cpu_level memory=$mem_level disk=$disk_level"
}

log_event "mixed_block_start" "schedule=$SCHEDULE_FILE cpu_cores=$CPU_CORES cpu_medium_workers=$CPU_MEDIUM cpu_high_workers=$CPU_HIGH cpu_very_high_workers=$CPU_VERY_HIGH disk_target_dir=$DISK_TARGET_DIR"

while IFS=',' read -r gap cpu_level mem_level disk_level duration; do
  gap=$(echo "$gap" | xargs)
  cpu_level=$(echo "$cpu_level" | xargs)
  mem_level=$(echo "$mem_level" | xargs)
  disk_level=$(echo "$disk_level" | xargs)
  duration=$(echo "$duration" | xargs)

  [ -z "${gap// }" ] && continue
  [ "$gap" = "gap_seconds" ] && continue

  sleep "$gap"
  run_mixed "$cpu_level" "$mem_level" "$disk_level" "$duration"
  sleep "$COOLDOWN_SECONDS"
done < "$SCHEDULE_FILE"

log_event "mixed_block_stop" "schedule=$SCHEDULE_FILE completed"