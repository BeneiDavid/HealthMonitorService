#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="${EVENTS_FILE:-events.csv}"
SCHEDULE_FILE="${1:?Usage: ./run_block_schedule.sh <schedule.csv>}"
RUN_ID="${2:-run_001}"
COOLDOWN_SECONDS="${3:-15}"

CPU_SCRIPT="./cpu_stress.sh"
MEM_SCRIPT="./memory_stress.sh"
DISK_SCRIPT="./disk_io_stress.sh"
DISK_TARGET_DIR="/tmp/thesis_stress"

log_event() {
  local event_type="$1"
  local details="${2:-}"
  local ts
  ts="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "${ts},${RUN_ID},${event_type},${details}" >> "$EVENTS_FILE"
}

clamp() {
  local value="$1"
  local min="$2"
  local max="$3"

  if [ "$value" -lt "$min" ]; then
    echo "$min"
  elif [ "$value" -gt "$max" ]; then
    echo "$max"
  else
    echo "$value"
  fi
}

# ---- Detect VM capacity at runtime ----
CPU_CORES="$(nproc)"
TOTAL_MEM_MB="$(free -m | awk '/Mem:/ {print $2}')"

# ---- Compute CPU levels ----
CPU_MEDIUM=$(( CPU_CORES / 2 ))
[ "$CPU_MEDIUM" -lt 1 ] && CPU_MEDIUM=1

CPU_HIGH=$(( CPU_CORES - 1 ))
[ "$CPU_HIGH" -lt 1 ] && CPU_HIGH=1

CPU_VERY_HIGH=$(( CPU_CORES - 1 )) # -- Same as CPU_HIGH for safeness, use longer duration when testing

# ---- Compute memory levels ----
MEM_MEDIUM=$(( TOTAL_MEM_MB * 60 / 100 ))
MEM_HIGH=$(( TOTAL_MEM_MB * 75 / 100 ))
MEM_VERY_HIGH=$(( TOTAL_MEM_MB * 85 / 100 ))

mkdir -p "$DISK_TARGET_DIR"

# ---- Detect free disk space on target filesystem ----
DISK_FREE_MB="$(df -Pm "$DISK_TARGET_DIR" | awk 'NR==2 {print $4}')"

# ---- Compute disk levels dynamically (relative to free space) ----
DISK_MEDIUM_RAW=$(( DISK_FREE_MB * 2 / 100 ))
DISK_HIGH_RAW=$(( DISK_FREE_MB * 5 / 100 ))
DISK_VERY_HIGH_RAW=$(( DISK_FREE_MB * 10 / 100 ))

# Clamp to safe, useful bounds
DISK_MEDIUM="$(clamp "$DISK_MEDIUM_RAW" 64 256)"
DISK_HIGH="$(clamp "$DISK_HIGH_RAW" 128 512)"
DISK_VERY_HIGH="$(clamp "$DISK_VERY_HIGH_RAW" 256 1024)"
resolve_cpu_workers() {
  local level="$1"
  case "$level" in
    medium) echo "$CPU_MEDIUM" ;;
    high) echo "$CPU_HIGH" ;;
    very_high) echo "$CPU_VERY_HIGH" ;;
    *) echo "Unknown CPU level: $level" >&2; exit 1 ;;
  esac
}

resolve_mem_mb() {
  local level="$1"
  case "$level" in
    medium) echo "$MEM_MEDIUM" ;;
    high) echo "$MEM_HIGH" ;;
    very_high) echo "$MEM_VERY_HIGH" ;;
    *) echo "Unknown memory level: $level" >&2; exit 1 ;;
  esac
}

resolve_disk_mb() {
  local level="$1"
  case "$level" in
    medium) echo "$DISK_MEDIUM" ;;
    high) echo "$DISK_HIGH" ;;
    very_high) echo "$DISK_VERY_HIGH" ;;
    *) echo "Unknown disk level: $level" >&2; exit 1 ;;
  esac
}

run_cpu() {
  local level="$1"
  local duration="$2"
  local workers
  workers="$(resolve_cpu_workers "$level")"

  log_event "scenario_start" "resource=cpu level=$level duration=${duration}s workers=$workers total_cores=$CPU_CORES"
  "$CPU_SCRIPT" "$RUN_ID" "$duration" "$workers"
  log_event "scenario_stop" "resource=cpu level=$level completed"
}

run_memory() {
  local level="$1"
  local duration="$2"
  local mb
  mb="$(resolve_mem_mb "$level")"

  log_event "scenario_start" "resource=memory level=$level duration=${duration}s memory_mb=$mb total_mem_mb=$TOTAL_MEM_MB"
  "$MEM_SCRIPT" "$RUN_ID" "$duration" "$mb"
  log_event "scenario_stop" "resource=memory level=$level completed"
}

run_disk() {
  local level="$1"
  local duration="$2"
  local file_mb
  file_mb="$(resolve_disk_mb "$level")"

  log_event "scenario_start" "resource=disk level=$level duration=${duration}s file_mb=$file_mb disk_free_mb=$DISK_FREE_MB"
  "$DISK_SCRIPT" "$RUN_ID" "$duration" "$DISK_TARGET_DIR" "$file_mb"
  log_event "scenario_stop" "resource=disk level=$level completed"
}

log_event "block_start" "schedule=$SCHEDULE_FILE cpu_cores=$CPU_CORES total_mem_mb=$TOTAL_MEM_MB disk_free_mb=$DISK_FREE_MB disk_medium_mb=$DISK_MEDIUM disk_high_mb=$DISK_HIGH disk_very_high_mb=$DISK_VERY_HIGH"

# skip header
tail -n +2 "$SCHEDULE_FILE" | while IFS=',' read -r gap resource level duration; do
  log_event "idle_period_start" "duration=${gap}s"
  sleep "$gap"
  log_event "idle_period_stop" "completed"

  case "$resource" in
    cpu)
      run_cpu "$level" "$duration"
      ;;
    memory)
      run_memory "$level" "$duration"
      ;;
    disk)
      run_disk "$level" "$duration"
      ;;
    *)
      echo "Unknown resource type: $resource" >&2
      exit 1
      ;;
  esac

  log_event "cooldown_start" "duration=${COOLDOWN_SECONDS}s"
  sleep "$COOLDOWN_SECONDS"
  log_event "cooldown_stop" "completed"
done

log_event "block_stop" "schedule=$SCHEDULE_FILE completed"