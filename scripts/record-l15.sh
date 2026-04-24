#!/usr/bin/env bash
# Launches the capture build and runs the deterministic Level 15 solve.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DEFAULT_APP="$PROJECT_DIR/Build/Capture/Windows/rescue-grid-capture.exe"
APP_PATH="${RESCUE_CAPTURE_APP_PATH:-$DEFAULT_APP}"

if [ ! -f "$APP_PATH" ]; then
    printf 'Capture app not found at %s\n' "$APP_PATH" >&2
    printf 'Build it first with scripts/build-capture.sh\n' >&2
    exit 1
fi

"$APP_PATH" -capture-l15 "$@"
