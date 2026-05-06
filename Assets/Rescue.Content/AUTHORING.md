# Level Authoring — Rescue Grid Phase 1

`docs/phase_1_spec.md` is authoritative for Phase 1 design and rules.

`Assets/Rescue.Content/README.md` maps the Rescue.Content level pipeline and points to the code/docs that own schema, validation, loader behavior, tooling, and design authority.

`.agents/skills/level-authoring/SKILL.md` is the Codex workflow/router for level authoring. It points agents to the relevant project docs and focused references before they author, review, or validate levels.

Executable behavior lives in code:

- `Assets/Rescue.Content/Schema.cs` defines the level content data model.
- `Assets/Rescue.Content/Validator.cs` defines core validation errors and warnings.
- `Assets/Rescue.Content/Phase1PolicyValidator.cs` defines Phase 1 packet policy warnings.
- `Assets/Rescue.Content/Loader.cs` defines how valid content becomes `GameState`.
- `Assets/Rescue.Content/Tuning.cs` defines content defaults and tuning mapping.

## Level files

Authored playable levels live in `Assets/StreamingAssets/Levels/`.

The current authored playable content contains `L00.json` through `L20.json`. Phase 1 packet-specific policy checks are driven by `docs/level-packets/phase1.packet.json`.

The filename must match the `id` field inside the JSON.

An explicit authoring template with the current standard fields is at [`scripts/level-template.json`](../../scripts/level-template.json). Copy it, rename it to the target level id, and fill in all values before authoring the tile grid.

## Required authoring gate

Before opening a PR that changes authored levels, briefs, solve scripts, golden paths, or level-authoring tools, run the full local gate:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-level-authoring.ps1
```

The same gate runs in CI. It checks level JSON validation, Phase 1 packet policy validation, solve verification, optional golden verification, brief/solve coverage for every level, telemetry bot smoke reports, and `Tools/LevelTelemetry.Tests`.

Every playable level JSON must have a matching `docs/level-briefs/<levelId>.brief.json` and `Assets/Resources/Levels/<levelId>.solve.json`. Golden paths are optional designer-approved paths; every committed `<levelId>.golden.json` must verify.

## ASCII symbol legend

Preview output uses the JSON tile-code grammar directly. No second symbol system.

| Symbol | Meaning |
|--------|---------|
| `.`    | Empty tile |
| `A`–`F`| Debris of that type |
| `CR`   | Crate (1 HP) |
| `CX`   | Reinforced crate (2 HP) — off by default in Phase 1 |
| `IA`–`IF` | Ice revealing debris of that type underneath |
| `V`    | Vine |
| `T0`–`T9` | Target with that id |
| `~`    | Flooded tile (rendered for `initialFloodedRows` bottom rows) |

Multi-character codes are padded to 2 characters and separated by a single space, so every column is the same visual width.

## Validate one level

Core validation checks JSON, schema, board consistency, runtime support, and general playability heuristics. Use this for any authored level, including content outside the Phase 1 packet.

Using the script wrapper:

```
./scripts/validate-levels.sh
```

Or directly against one file:

```
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate Assets/StreamingAssets/Levels/L03.json
```

Exit code `0` = valid (warnings may still print). Exit code `1` = errors. Exit code `2` = bad invocation.

## Validate Phase 1 packet policy

Phase 1 policy validation runs core validation plus manifest-driven packet warnings, such as Dock Jam placement, debris pool bands, static vine intro setup, Phase 1 water interval floor, reinforced crate usage, and spawn-integrity exception notes.

Use it when editing or reviewing the Phase 1 packet:

```
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1 Assets/StreamingAssets/Levels/L03.json
```

## Preview a level

Using the helper script:

```
./scripts/preview-level.sh L03
```

Or directly:

```
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- preview Assets/StreamingAssets/Levels/L03.json
```

Output is the ASCII board preceded by a header line:

```
L03 — Rescue order arrives  [6×7]  water:10  flooded:0
.  .  A  B  .  .
...
```

Preview exits non-zero if validation has errors, so you always see validation output first.

## Design review after preview

Validation passing is required but not sufficient.

After previewing a level, review it against:

- `docs/phase_1_spec.md`
- `.agents/skills/level-authoring/SKILL.md`
- `.agents/skills/level-authoring/references/DENSITY_AND_READABILITY.md`
- the level's `meta.intent`, `meta.expectedPath`, and `meta.expectedFailMode`

Check:

- Does the opening board look visually complete?
- Are empty cells justified by route, hazard, geometry, spawn corridor, target readability, or mobile readability?
- Is the intended first move readable without making the board look like a tutorial diagram?
- Does the expected path avoid relying on lucky spawn?
- Is the expected failure mode fair and attributable?
- Does the level end emotionally on rescue?

If a level is intentionally sparse or deviates from tuning tables, document the reason in `meta.notes`.

## Validate all levels

```
./scripts/validate-levels.sh
```

Or directly:

```
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels
```

This runs core `validate-all` over the entire `Assets/StreamingAssets/Levels/` directory and prints a summary. Exit code `0` only if every level passes.

For the current Phase 1 packet, also run:

```
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1-all Assets/StreamingAssets/Levels
```

Use core validation for general content validity. Use Phase 1 policy validation when the content is expected to remain aligned with the configured Phase 1 packet manifest.

## Verify solve scripts

Solve/replay scripts live in `Assets/Resources/Levels/` as `<levelId>.solve.json`.

Use the solve authoring tool to verify that solve scripts still match current level behavior:

```bash
dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves
```

Run this after changing level JSON, rules, loader behavior, or solve scripts.

If a solve script mismatch appears, determine whether the level changed, the script is stale, or the expected result is wrong.

## Generate difficulty telemetry

Telemetry reports are offline design diagnostics, not runtime analytics.

Run one level:

```bash
dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --level L01
```

Run onboarding:

```bash
dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --range L00-L20 --samples 200 --max-actions 30
```

Reports are written to `Reports/LevelTelemetry/` by default.

Use telemetry to compare bot behavior, loss reasons, target progress events, dock overflow frequency, water loss frequency, and whether rescue-focused play outperforms generic clearing.

Telemetry does not replace human playtest.

Offline bot telemetry is the authoring-gate surface. Runtime telemetry sessions are emitted by the development debug panel for playtest/replay capture; the main player session is not wired to runtime telemetry by default.

## Verify golden paths

Golden paths live in `Assets/Resources/Levels/` as `<levelId>.golden.json`.

They are designer-approved intended solves, separate from solver-found `<levelId>.solve.json` files.

Run:

```bash
dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-golden
```

Golden verification checks action validity, expected outcome, expected ordered events, optional target extraction order, and max action count.

## Watch mode

The watcher re-validates and re-previews any level file the moment you save it.

**Install the required dependency first:**

```
# macOS
brew install fswatch

# Ubuntu / WSL
sudo apt-get install inotify-tools
```

Then start the watcher:

```
./scripts/watch-levels.sh
```

On each `.json` save the watcher:

1. Runs `validate` and prints errors/warnings to the terminal.
2. Writes `Assets/StreamingAssets/Levels/<id>_validation.log` (exit code + errors + timestamp).
3. Runs `preview` and prints the ASCII board to the terminal.
4. Writes `Assets/StreamingAssets/Levels/<id>_preview.txt`.

Both output files are in `.gitignore` — they are disposable authoring artefacts, not content.

The watcher handles both normal saves and atomic swap-write saves (VS Code default, some vim configs).

## Commit workflow

For individual level edits, prefer one level per commit.

Format:

```
level: L03 — rescue order arrives
```

For documentation, tooling, template, or coordinated level-set changes, use a focused commit that describes the scope.

If the level deviates from the tuning tables in the design spec, document the deviation in `meta.notes`.

Set `meta.isRuleTeach = true` only on `L00`. That flag is reserved for the Level 0 rule-teach and causes the loader to hold the waterline in its pre-action state until the first valid action.
