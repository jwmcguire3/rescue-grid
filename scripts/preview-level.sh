#!/usr/bin/env bash
# Usage: preview-level.sh <id>
# Resolves Assets/StreamingAssets/Levels/<id>.json, validates it, and prints the ASCII board.
# Exits non-zero if the file does not exist or validation has errors.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
VALIDATOR_PROJECT="$PROJECT_DIR/Tools/LevelValidator/LevelValidator.csproj"

if [ $# -lt 1 ]; then
    printf 'Usage: preview-level.sh <id>\n' >&2
    printf 'Example: preview-level.sh L03\n' >&2
    exit 2
fi

ID="$1"
FILE="$PROJECT_DIR/Assets/StreamingAssets/Levels/${ID}.json"

if [ ! -f "$FILE" ]; then
    printf 'Level file not found: %s\n' "$FILE" >&2
    exit 1
fi

dotnet run --project "$VALIDATOR_PROJECT" -- preview "$FILE"
