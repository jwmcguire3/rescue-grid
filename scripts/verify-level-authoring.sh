#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CONTINUE_ON_ERROR=0

usage() {
  echo "Usage: $0 [--project-path <path>] [--continue-on-error]"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project-path)
      if [[ $# -lt 2 ]]; then
        usage
        exit 2
      fi
      PROJECT_DIR="$(cd "$2" && pwd)"
      shift 2
      ;;
    --continue-on-error)
      CONTINUE_ON_ERROR=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

LEVELS_DIR="$PROJECT_DIR/Assets/StreamingAssets/Levels"
BRIEFS_DIR="$PROJECT_DIR/docs/level-briefs"
RESOURCES_DIR="$PROJECT_DIR/Assets/Resources/Levels"
MANIFEST_PATH="$PROJECT_DIR/docs/level-packets/phase1.packet.json"
LOG_DIR="$PROJECT_DIR/Reports/LevelAuthoringGate"

for required in "$LEVELS_DIR" "$BRIEFS_DIR" "$RESOURCES_DIR"; do
  if [[ ! -d "$required" ]]; then
    echo "Required directory was not found: $required" >&2
    exit 1
  fi
done

if [[ ! -f "$MANIFEST_PATH" ]]; then
  echo "Required packet manifest was not found: $MANIFEST_PATH" >&2
  exit 1
fi

mkdir -p "$LOG_DIR"
cd "$PROJECT_DIR" || exit 1

STAGE_NAMES=()
STAGE_COMMANDS=()
STAGE_DETAILS=()
STAGE_STATUSES=()
STAGE_WARNINGS=()

add_stage() {
  STAGE_NAMES+=("$1")
  STAGE_COMMANDS+=("$2")
  STAGE_DETAILS+=("$3")
}

add_stage "validate-all" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels"
add_stage "validate-phase1-all" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1-all Assets/StreamingAssets/Levels" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1-all Assets/StreamingAssets/Levels"
add_stage "validate-brief-all" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-brief-all Assets/StreamingAssets/Levels docs/level-briefs" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-brief-all Assets/StreamingAssets/Levels docs/level-briefs"
add_stage "readability-all" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- readability-all Assets/StreamingAssets/Levels docs/level-briefs" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- readability-all Assets/StreamingAssets/Levels docs/level-briefs"
add_stage "design-report-all" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- design-report-all Assets/StreamingAssets/Levels docs/level-briefs" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- design-report-all Assets/StreamingAssets/Levels docs/level-briefs"
add_stage "packet-design-report" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- packet-report docs/level-packets/phase1.packet.json Assets/StreamingAssets/Levels docs/level-briefs" \
  "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- packet-report docs/level-packets/phase1.packet.json Assets/StreamingAssets/Levels docs/level-briefs"
add_stage "verify-solves" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves"
add_stage "verify-golden" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-golden" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-golden"
add_stage "verify-failpaths" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-failpaths" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-failpaths"
add_stage "compare-assistance-all" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --compare-assistance-all" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --compare-assistance-all"
add_stage "packet-report" \
  "dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-all" \
  "dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-all"
add_stage "verify-acceptance" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-acceptance --manifest docs/level-packets/phase1.packet.json --levels-dir Assets/StreamingAssets/Levels --briefs-dir docs/level-briefs --resources-dir Assets/Resources/Levels" \
  "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-acceptance --manifest docs/level-packets/phase1.packet.json --levels-dir Assets/StreamingAssets/Levels --briefs-dir docs/level-briefs --resources-dir Assets/Resources/Levels"

print_summary() {
  echo ""
  echo "Level authoring gate summary"
  printf "%-29s %-7s %-9s %s\n" "Stage" "Status" "Warnings" "Inspect"
  printf "%-29s %-7s %-9s %s\n" "-----" "------" "--------" "-------"

  local failed=0
  for i in "${!STAGE_STATUSES[@]}"; do
    printf "%-29s %-7s %-9s %s\n" "${STAGE_NAMES[$i]}" "${STAGE_STATUSES[$i]}" "${STAGE_WARNINGS[$i]}" "${STAGE_DETAILS[$i]}"
    if [[ "${STAGE_STATUSES[$i]}" == "FAIL" ]]; then
      failed=1
    fi
  done

  echo ""
  if [[ "$failed" -eq 0 ]]; then
    echo "Level authoring gate passed."
  else
    echo "Level authoring gate failed."
  fi
}

echo "Verifying Phase 1 level packet for design review / playtest build..."
echo "Project: $PROJECT_DIR"
echo "Manifest: docs/level-packets/phase1.packet.json"

for i in "${!STAGE_NAMES[@]}"; do
  name="${STAGE_NAMES[$i]}"
  command="${STAGE_COMMANDS[$i]}"
  log_path="$LOG_DIR/${name//[^A-Za-z0-9._-]/_}.log"

  echo ""
  echo "== $name =="

  if [[ "$name" == "verify-failpaths" ]] && ! compgen -G "$RESOURCES_DIR/*.fail.json" > /dev/null; then
    echo "SKIP: No .fail.json files found under Assets/Resources/Levels."
    STAGE_STATUSES+=("PASS")
    STAGE_WARNINGS+=("0")
    continue
  fi

  echo "> $command"
  set +e
  bash -lc "$command" 2>&1 | tee "$log_path"
  exit_code="${PIPESTATUS[0]}"
  set -e

  warning_count="$(grep -Eic '\b(warning|warn)\b' "$log_path" || true)"
  STAGE_WARNINGS+=("$warning_count")

  if [[ "$exit_code" -eq 0 ]]; then
    STAGE_STATUSES+=("PASS")
  else
    STAGE_STATUSES+=("FAIL")
    if [[ "$CONTINUE_ON_ERROR" -eq 0 ]]; then
      print_summary
      exit "$exit_code"
    fi
  fi
done

print_summary
for status in "${STAGE_STATUSES[@]}"; do
  if [[ "$status" == "FAIL" ]]; then
    exit 1
  fi
done
