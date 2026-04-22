#!/usr/bin/env bash
# Builds an iOS Xcode project with Unity batchmode.
#
# Required environment:
#   UNITY_PATH: absolute path to the Unity editor executable, or `Unity` on PATH
#
# Expected toolchain:
#   - macOS host
#   - Unity 6.4 with iOS Build Support installed
#   - Xcode command line tools installed (`xcodebuild` available)
#
# Optional environment:
#   RESCUE_BUILD_OUTPUT_ROOT: output root directory (default: Build)
#   RESCUE_BUILD_APP_IDENTIFIER: shared bundle id base (default from BuildScripts.cs)
#   RESCUE_IOS_BUNDLE_IDENTIFIER: overrides the iOS bundle identifier
#   RESCUE_DEVELOPMENT_BUILD: 1/0, defaults to 1
#   RESCUE_ALLOW_DEBUGGING: 1/0, defaults to 1
#   RESCUE_CONNECT_PROFILER: 1/0, defaults to 0
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_ROOT="${RESCUE_BUILD_OUTPUT_ROOT:-$PROJECT_DIR/Build}"
LOG_DIR="$OUTPUT_ROOT/Logs"
LOG_FILE="$LOG_DIR/build-ios.log"

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

[ "$(uname -s)" = "Darwin" ] || fail "iOS builds require macOS because Unity exports an Xcode project."
command -v xcodebuild >/dev/null 2>&1 || fail "xcodebuild was not found. Install Xcode and its command line tools first."

UNITY_BIN="$(resolve_unity)"
mkdir -p "$LOG_DIR"

"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -buildTarget iOS \
    -executeMethod BuildScripts.BuildIos \
    -logFile "$LOG_FILE" \
    -quit

printf 'iOS Xcode project written to %s/iOS/XcodeProject\n' "$OUTPUT_ROOT"
printf 'Unity log written to %s\n' "$LOG_FILE"
