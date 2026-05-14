# Rescue.Content

`Rescue.Content` reads authored level JSON, validates it, previews it, applies content/tuning defaults, and loads it into `Rescue.Core.State.GameState`. Levels are authored as JSON, not Unity scenes.

## Authority map

- Schema/types: `Assets/Rescue.Content/Schema.cs`
- JSON serialization/deserialization: `Assets/Rescue.Content/ContentJson.cs`
- Core validation rules/warnings: `Assets/Rescue.Content/Validator.cs`
- Phase 1 packet policy warnings: `Assets/Rescue.Content/Phase1PolicyValidator.cs`
- Runtime loading into `GameState`: `Assets/Rescue.Content/Loader.cs`
- ASCII preview: `Assets/Rescue.Content/AsciiPreview.cs`
- SVG preview: `Assets/Rescue.Content/LevelSvgPreview.cs`
- Readability metrics/policy: `Assets/Rescue.Content/LevelReadabilityAnalyzer.cs` and `Assets/Rescue.Content/ReadabilityPolicyValidator.cs`
- Single-level design reports/review markdown: `Assets/Rescue.Content/LevelDesignReportBuilder.cs` and `Assets/Rescue.Content/LevelReviewWriter.cs`
- Packet design reports: `Assets/Rescue.Content/LevelPacketDesignReportBuilder.cs`
- Tuning defaults/override mapping: `Assets/Rescue.Content/Tuning.cs`
- Phase 1 packet manifest: `docs/level-packets/phase1.packet.json`
- Level briefs: `docs/level-briefs/`
- Level review markdown: `docs/level-reviews/`
- Authoring/tool workflow: `Assets/Rescue.Content/AUTHORING.md`
- Phase 1 design/rules/tuning: `docs/phase_1_spec.md`
- Codex/agent workflow: `.agents/skills/level-authoring/SKILL.md`
- Authoring template: `scripts/level-template.json`
- Validation/readability/design-report CLI: `Tools/LevelValidator`
- Solve/golden/fail-path/assistance/acceptance CLI: `Tools/SolveAuthoring`
- Packet replay summary CLI: `Tools/LevelTelemetry`

## Authored level storage

Authored playable levels live in `Assets/StreamingAssets/Levels/`. The current Phase 1 packet content is `L00.json` through `L40.json`; the packet manifest defines which authored files are packet-facing.

Playable JSON remains the source of layout truth. Briefs, solve scripts, golden paths, fail paths, telemetry summaries, and review markdown verify, explain, or approve a level; they do not define the board layout.

Unity `.meta` files are asset metadata, not level definitions.

## Solve/replay script storage

Solve and replay scripts live in `Assets/Resources/Levels/`. The current Phase 1 packet pattern is `L00.solve.json` through `L40.solve.json`.

These scripts verify expected behavior. They are not the source of layout truth.

## Tooling

Validation is the first gate, not acceptance. Use `Assets/Rescue.Content/AUTHORING.md` for the exact workflow and command examples.

`Tools/LevelValidator` owns level, brief, readability, preview, design-report, packet-design-report, and review-markdown commands:

- `validate`, `validate-all`: core JSON/schema/content validation.
- `validate-phase1`, `validate-phase1-all`: core validation plus Phase 1 packet policy checks.
- `validate-brief`, `validate-brief-all`: brief schema and brief-to-level conformance.
- `preview`, `preview-svg`: ASCII and SVG layout previews.
- `readability`, `readability-all`: density/readability metrics and policy warnings.
- `design-report`, `design-report-all`: designer-facing single-level or batch reports.
- `packet-report`: manifest-ordered packet design pacing report.
- `write-review`, `write-review-all`: generated review markdown under `docs/level-reviews/`.

`Tools/SolveAuthoring` owns replay proof and acceptance commands:

- `--verify-solves`: committed solve script verification.
- `--verify-golden`: designer-approved golden path verification.
- `--verify-failpaths`: expected fail-path verification when fail paths exist.
- `--compare-assistance`, `--compare-assistance-all`: assisted vs unassisted solve comparison.
- `--verify-acceptance`: manifest-driven packet acceptance gate.

`Tools/LevelTelemetry` owns replay summaries:

- `summarize-level`, `summarize-all`: summarize committed solve, golden, and optional fail replay artifacts.

Wrapper scripts:

- `scripts/validate-levels.sh`
- `scripts/preview-level.sh`
- `scripts/preview-level-svg.sh`
- `scripts/watch-levels.sh`
- `scripts/verify-level-authoring.ps1`
- `scripts/verify-level-authoring.sh`

## Runtime consumers

Runtime gameplay loads by level id through `Loader.LoadLevel(...)` from `Assets/StreamingAssets/Levels/`.

The debug panel exposes the Phase 1 packet list for normal packet playback. Direct loading by id can still load authored levels that exist outside the packet.

Smoke, replay, and capture tools consume solve scripts from `Assets/Resources/Levels/`.

## Current content status

The current Phase 1 packet content is `L00` through `L40`. Phase 1 packet policy checks are driven by `docs/level-packets/phase1.packet.json`.

Do not hardcode stale validation results here. If status needs to be reported, run:

```bash
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1-all Assets/StreamingAssets/Levels
dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves
```

## What not to put here

- Do not duplicate the full schema; use `Schema.cs`.
- Do not duplicate validator logic; use `Validator.cs` for core content validation and `Phase1PolicyValidator.cs` for Phase 1 packet policy warnings.
- Do not duplicate loader behavior; use `Loader.cs`.
- Do not duplicate Phase 1 design rules; use `docs/phase_1_spec.md`.
- Do not duplicate authoring workflow; use `AUTHORING.md`.
