#!/usr/bin/env bash
# Builds the special capture variant.
#
# Required environment:
#   UNITY_PATH: absolute path to the Unity editor executable, or `Unity` on PATH
#
# Optional environment:
#   RESCUE_CAPTURE_TARGET: windows (default), android, ios, or webgl
#   RESCUE_BUILD_OUTPUT_ROOT: output root directory (default: Build)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_ROOT="${RESCUE_BUILD_OUTPUT_ROOT:-$PROJECT_DIR/Build}"
LOG_DIR="$OUTPUT_ROOT/Logs"
LOG_FILE="$LOG_DIR/build-capture.log"
TARGET_NAME="${RESCUE_CAPTURE_TARGET:-windows}"

fail() {
    printf '%s\n' "$1" >&2
    exit 1
}

resolve_unity() {
    if [ -n "${UNITY_PATH:-}" ]; then
        [ -x "$UNITY_PATH" ] || fail "UNITY_PATH is set but not executable: $UNITY_PATH"
        printf '%s\n' "$UNITY_PATH"
        return
    fi

    if command -v Unity >/dev/null 2>&1; then
        command -v Unity
        return
    fi

    fail "Unity editor not found. Set UNITY_PATH or add Unity to PATH."
}

UNITY_BIN="$(resolve_unity)"
mkdir -p "$LOG_DIR"

case "${TARGET_NAME,,}" in
    windows|win|win64)
        EXPECTED_ARTIFACT="$OUTPUT_ROOT/Capture/Windows/rescue-grid-capture.exe"
        ;;
    android)
        EXPECTED_ARTIFACT="$OUTPUT_ROOT/Capture/Android/rescue-grid-capture.apk"
        ;;
    ios)
        EXPECTED_ARTIFACT="$OUTPUT_ROOT/Capture/iOS/XcodeProject"
        ;;
    web|webgl)
        EXPECTED_ARTIFACT="$OUTPUT_ROOT/Capture/WebGL"
        ;;
    *)
        fail "RESCUE_CAPTURE_TARGET must be one of: windows, android, ios, webgl."
        ;;
esac

export RESCUE_DEVELOPMENT_BUILD=0
export RESCUE_ALLOW_DEBUGGING=0
export RESCUE_CONNECT_PROFILER=0
export RESCUE_CAPTURE_TARGET="$TARGET_NAME"

"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -executeMethod BuildScripts.BuildCapture \
    -logFile "$LOG_FILE" \
    -quit

if [ ! -e "$EXPECTED_ARTIFACT" ]; then
    fail "Unity exited without producing the expected capture artifact: $EXPECTED_ARTIFACT"
fi

printf 'Capture build produced %s\n' "$EXPECTED_ARTIFACT"
printf 'Unity log written to %s\n' "$LOG_FILE"
