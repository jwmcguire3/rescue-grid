#!/usr/bin/env bash
# Watch Assets/StreamingAssets/Levels/*.json for changes.
# On each change: runs the validator, writes a log, runs preview, writes a txt.
# Requires fswatch (macOS) or inotifywait (Linux). Fails with a clear message if neither is available.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LEVELS_DIR="$PROJECT_DIR/Assets/StreamingAssets/Levels"
VALIDATOR_PROJECT="$PROJECT_DIR/Tools/LevelValidator/LevelValidator.csproj"

log()  { printf '%s\n' "$*"; }
err()  { printf '%s\n' "$*" >&2; }

run_for_file() {
    local file="$1"
    local base
    base="$(basename "$file" .json)"
    local log_file="$LEVELS_DIR/${base}_validation.log"
    local preview_file="$LEVELS_DIR/${base}_preview.txt"
    local timestamp
    timestamp="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

    log ""
    log "=== $base  [$timestamp] ==="

    # Validation
    local exit_code=0
    local val_output
    val_output="$(dotnet run --project "$VALIDATOR_PROJECT" -- validate "$file" 2>&1)" || exit_code=$?

    log "$val_output"

    {
        printf 'timestamp: %s\n' "$timestamp"
        printf 'exit_code: %d\n' "$exit_code"
        printf '%s\n' "$val_output"
    } > "$log_file"

    # Preview (only attempt if JSON parses; validator exits 2 on parse error)
    if [ "$exit_code" -ne 2 ]; then
        local preview_output
        preview_output="$(dotnet run --project "$VALIDATOR_PROJECT" -- preview "$file" 2>&1)" || true
        log "$preview_output"
        printf '%s\n' "$preview_output" > "$preview_file"
    fi
}

# Detect which watcher is available.
if command -v fswatch >/dev/null 2>&1; then
    log "Using fswatch. Watching $LEVELS_DIR ..."
    # -o: coalesce events; -e: exclude pattern (skip log/txt artefacts); -r: recursive off (flat dir)
    fswatch -o -e '.*_validation\.log$' -e '.*_preview\.txt$' "$LEVELS_DIR" | while read -r _; do
        # fswatch fires on any change in the dir; find files modified in the last 2 seconds.
        while IFS= read -r -d '' changed; do
            [[ "$changed" == *.json ]] || continue
            run_for_file "$changed"
        done < <(find "$LEVELS_DIR" -maxdepth 1 -name '*.json' -newer "$LEVELS_DIR" -print0 2>/dev/null)
    done

elif command -v inotifywait >/dev/null 2>&1; then
    log "Using inotifywait. Watching $LEVELS_DIR ..."
    # close_write covers normal saves; moved_to + create covers atomic swap-writes (VS Code, vim).
    inotifywait -m -e close_write,moved_to,create --format '%w%f' "$LEVELS_DIR" 2>/dev/null | while IFS= read -r changed; do
        [[ "$changed" == *.json ]] || continue
        # Skip generated artefacts (shouldn't match *.json but guard anyway).
        [[ "$changed" == *_validation.log ]] && continue
        [[ "$changed" == *_preview.txt ]] && continue
        run_for_file "$changed"
    done

else
    err ""
    err "ERROR: Neither fswatch nor inotifywait was found."
    err ""
    err "Install one of the following before running this script:"
    err ""
    err "  macOS:   brew install fswatch"
    err "  Ubuntu:  sudo apt-get install inotify-tools"
    err ""
    exit 1
fi
