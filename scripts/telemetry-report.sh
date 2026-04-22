#!/usr/bin/env bash
# Usage:
#   ./scripts/telemetry-report.sh <path-or-dir>
#
# If <path-or-dir> is a file, runs: TelemetryReport report <file>
# If <path-or-dir> is a directory, runs: TelemetryReport report-all <dir>

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TOOL_DIR="$REPO_ROOT/Tools/TelemetryReport"

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <session-jsonl-path-or-telemetry-dir>" >&2
  exit 1
fi

TARGET="$1"

if [ -f "$TARGET" ]; then
  COMMAND="report"
elif [ -d "$TARGET" ]; then
  COMMAND="report-all"
else
  echo "Error: '$TARGET' is neither a file nor a directory." >&2
  exit 1
fi

dotnet run --project "$TOOL_DIR/TelemetryReport.csproj" --configuration Release -- "$COMMAND" "$TARGET"
