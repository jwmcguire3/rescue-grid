#!/usr/bin/env bash
# Usage: preview-level-svg.sh <id> [output-svg-path]
# Resolves Assets/StreamingAssets/Levels/<id>.json, validates it, and writes a simple SVG snapshot.
# Exits non-zero if the file does not exist or validation has errors.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
VALIDATOR_PROJECT="$PROJECT_DIR/Tools/LevelValidator/LevelValidator.csproj"

if [ $# -lt 1 ]; then
    printf 'Usage: preview-level-svg.sh <id> [output-svg-path]\n' >&2
    printf 'Example: preview-level-svg.sh L03\n' >&2
    exit 2
fi

ID="$1"
FILE="$PROJECT_DIR/Assets/StreamingAssets/Levels/${ID}.json"
OUTPUT="${2:-$PROJECT_DIR/Assets/StreamingAssets/Levels/${ID}_preview.svg}"

if [ ! -f "$FILE" ]; then
    printf 'Level file not found: %s\n' "$FILE" >&2
    exit 1
fi

dotnet run --project "$VALIDATOR_PROJECT" -- preview-svg "$FILE" "$OUTPUT"
