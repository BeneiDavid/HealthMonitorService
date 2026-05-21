#!/usr/bin/env bash
set -euo pipefail

EVENTS_FILE="$1"
TEST_RUN_ID="$2"
EVENT_TYPE="$3"
DETAILS="${4:-}"

TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "${TIMESTAMP},${TEST_RUN_ID},${EVENT_TYPE},${DETAILS}" >> "$EVENTS_FILE"