#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LEVELS_DIR="${1:-$PROJECT_DIR/Assets/StreamingAssets/Levels}"

dotnet run --project "$PROJECT_DIR/Tools/LevelValidator/LevelValidator.csproj" -- validate-all "$LEVELS_DIR"
