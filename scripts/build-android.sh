#!/usr/bin/env bash
# Builds an Android dev package with Unity batchmode.
#
# Required environment:
#   UNITY_PATH: absolute path to the Unity editor executable, or `Unity` on PATH
#
# Expected toolchain:
#   - Unity 6.4 with Android Build Support installed
#   - Android SDK / NDK / OpenJDK configured in Unity, or available to Unity on this machine
#
# Optional environment:
#   RESCUE_BUILD_OUTPUT_ROOT: output root directory (default: Build)
#   RESCUE_ANDROID_FORMAT: apk or aab (default: apk)
#   RESCUE_ANDROID_OUTPUT_NAME: output filename override
#   RESCUE_BUILD_APP_IDENTIFIER: shared application id base
#   RESCUE_ANDROID_APPLICATION_IDENTIFIER: overrides Android application id
#   RESCUE_DEVELOPMENT_BUILD: 1/0, defaults to 1
#   RESCUE_ALLOW_DEBUGGING: 1/0, defaults to 1
#   RESCUE_CONNECT_PROFILER: 1/0, defaults to 0
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_ROOT="${RESCUE_BUILD_OUTPUT_ROOT:-$PROJECT_DIR/Build}"
LOG_DIR="$OUTPUT_ROOT/Logs"
LOG_FILE="$LOG_DIR/build-android.log"
ANDROID_FORMAT="${RESCUE_ANDROID_FORMAT:-apk}"

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

case "$ANDROID_FORMAT" in
    apk|aab) ;;
    *) fail "RESCUE_ANDROID_FORMAT must be 'apk' or 'aab'." ;;
esac

if [ -z "${ANDROID_SDK_ROOT:-}" ] && [ -z "${ANDROID_HOME:-}" ]; then
    printf '%s\n' "ANDROID_SDK_ROOT / ANDROID_HOME not set; continuing because Unity may be configured with embedded Android tools." >&2
fi

UNITY_BIN="$(resolve_unity)"
mkdir -p "$LOG_DIR"

"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -buildTarget Android \
    -executeMethod BuildScripts.BuildAndroid \
    -logFile "$LOG_FILE" \
    -quit

printf 'Android build written under %s/Android\n' "$OUTPUT_ROOT"
printf 'Unity log written to %s\n' "$LOG_FILE"
