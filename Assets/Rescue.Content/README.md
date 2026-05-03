# Rescue.Content

`Rescue.Content` reads authored level JSON, validates it, previews it, applies content/tuning defaults, and loads it into `Rescue.Core.State.GameState`. Levels are authored as JSON, not Unity scenes.

## Authority map

- Schema/types: `Assets/Rescue.Content/Schema.cs`
- JSON serialization/deserialization: `Assets/Rescue.Content/ContentJson.cs`
- Validation rules/warnings: `Assets/Rescue.Content/Validator.cs`
- Runtime loading into `GameState`: `Assets/Rescue.Content/Loader.cs`
- ASCII preview: `Assets/Rescue.Content/AsciiPreview.cs`
- Tuning defaults/override mapping: `Assets/Rescue.Content/Tuning.cs`
- Authoring/tool workflow: `Assets/Rescue.Content/AUTHORING.md`
- Phase 1 design/rules/tuning: `docs/phase_1_spec.md`
- Codex/agent workflow: `.agents/skills/level-authoring/SKILL.md`
- Authoring template: `scripts/level-template.json`

## Authored level storage

Authored playable levels live in `Assets/StreamingAssets/Levels/`. The current Phase 1 packet is `L00.json` through `L15.json`, and these files are the authoritative playable level definitions.

Unity `.meta` files are asset metadata, not level definitions.

## Solve/replay script storage

Solve and replay scripts live in `Assets/Resources/Levels/`. The current pattern is `L00.solve.json` through `L15.solve.json`.

These scripts verify expected behavior. They are not the source of layout truth.

## Tooling

`Tools/LevelValidator` supports `validate`, `validate-all`, and `preview`.

`Tools/SolveAuthoring` verifies solve scripts with `--verify-solves`.

Wrapper scripts:

- `scripts/validate-levels.sh`
- `scripts/preview-level.sh`
- `scripts/watch-levels.sh`

Use `Assets/Rescue.Content/AUTHORING.md` for the exact authoring and validation workflow.

## Runtime consumers

Runtime gameplay loads by level id through `Loader.LoadLevel(...)` from `Assets/StreamingAssets/Levels/`.

The debug panel discovers and loads levels from `Assets/StreamingAssets/Levels/` when present in the repo.

Smoke, replay, and capture tools consume solve scripts from `Assets/Resources/Levels/`.

## Current content status

The current authored Phase 1 packet is `L00` through `L15`.

Do not hardcode stale validation results here. If status needs to be reported, run:

```bash
dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels
dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves
```

## What not to put here

- Do not duplicate the full schema; use `Schema.cs`.
- Do not duplicate validator logic; use `Validator.cs`.
- Do not duplicate loader behavior; use `Loader.cs`.
- Do not duplicate Phase 1 design rules; use `docs/phase_1_spec.md`.
- Do not duplicate authoring workflow; use `AUTHORING.md`.
