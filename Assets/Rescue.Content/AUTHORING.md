# Level Authoring — Rescue Grid Phase 1

The authoritative design guide is at [`.agents/skills/level-authoring/SKILL.md`](../../.agents/skills/level-authoring/SKILL.md). Read it before authoring or modifying any level. This file documents the tooling that the skill references.

## Level files

Levels live in `Assets/StreamingAssets/Levels/` as `L01.json` through `L15.json`. The filename must match the `id` field inside the JSON.

A complete template with every required field is at [`scripts/level-template.json`](../../scripts/level-template.json). Copy it, rename it to the target level id, and fill in all values before authoring the tile grid.

## ASCII symbol legend

Preview output uses the JSON tile-code grammar directly. No second symbol system.

| Symbol | Meaning |
|--------|---------|
| `.`    | Empty tile |
| `A`–`E`| Debris of that type |
| `CR`   | Crate (1 HP) |
| `CX`   | Reinforced crate (2 HP) — off by default in Phase 1 |
| `IA`–`IE` | Ice revealing debris of that type underneath |
| `V`    | Vine |
| `T0`–`T9` | Target with that id |
| `~`    | Flooded tile (rendered for `initialFloodedRows` bottom rows) |

Multi-character codes are padded to 2 characters and separated by a single space, so every column is the same visual width.

## Validate one level

Using the script wrapper:

```
./scripts/validate-levels.sh
```

Or directly against one file:

```
dotnet run --project Tools/LevelValidator -- validate Assets/StreamingAssets/Levels/L03.json
```

Exit code `0` = valid (warnings may still print). Exit code `1` = errors. Exit code `2` = bad invocation.

## Preview a level

Using the helper script:

```
./scripts/preview-level.sh L03
```

Or directly:

```
dotnet run --project Tools/LevelValidator -- preview Assets/StreamingAssets/Levels/L03.json
```

Output is the ASCII board preceded by a header line:

```
L03 — Rescue order arrives  [6×7]  water:10  flooded:0
.  .  A  B  .  .
...
```

Preview exits non-zero if validation has errors, so you always see validation output first.

## Validate all levels

```
./scripts/validate-levels.sh
```

This runs `validate-all` over the entire `Assets/StreamingAssets/Levels/` directory and prints a summary. Exit code `0` only if every level passes.

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

One level per commit. Format:

```
level: L03 — rescue order arrives
```

If the level deviates from the tuning tables in the design spec, document the deviation in `meta.notes`.
