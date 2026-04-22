#!/usr/bin/env bash
# Usage:
#   ./scripts/replay.sh --session path/to/session.jsonl
#   ./scripts/replay.sh --session path/to/session.jsonl --step 12
#   ./scripts/replay.sh --session path/to/session.jsonl --export out.json

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TOOL_DIR="$REPO_ROOT/Tools/Replay"

dotnet run --project "$TOOL_DIR/Replay.csproj" --configuration Release -- "$@"
