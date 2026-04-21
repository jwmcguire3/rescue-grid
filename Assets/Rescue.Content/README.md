# Rescue.Content Level Schema

`Rescue.Content` defines the Phase 1 level JSON contract, the pure validator, and the loader that converts validated content into `Rescue.Core.State.GameState`.

## File shape

```json
{
  "id": "L03",
  "name": "Rescue order arrives",
  "board": {
    "width": 6,
    "height": 7,
    "tiles": [[".", ".", ".", ".", ".", "."]]
  },
  "debrisTypePool": ["A", "B", "C", "D"],
  "baseDistribution": {
    "A": 1.0,
    "B": 1.0,
    "C": 1.0,
    "D": 1.0
  },
  "targets": [
    { "id": "0", "row": 2, "col": 3 }
  ],
  "initialFloodedRows": 0,
  "water": {
    "riseInterval": 10
  },
  "vine": {
    "growthThreshold": 4,
    "growthPriority": [
      { "row": 3, "col": 4 }
    ]
  },
  "dock": {
    "size": 7,
    "jamEnabled": false
  },
  "assistance": {
    "chance": 0.7,
    "consecutiveEmergencyCap": 2
  },
  "meta": {
    "intent": "Teach rescue order pressure.",
    "expectedPath": "Save the lower target first.",
    "expectedFailMode": "Save the easy target first and lose the urgent one.",
    "whatItProves": "Rescue order is the puzzle.",
    "notes": "Optional author notes."
  }
}
```

## Tile codes

- `.`: empty tile
- `A`..`E`: debris tiles
- `CR`: crate with 1 HP
- `CX`: reinforced crate with 2 HP (off by default in Phase 1)
- `I<X>`: ice with hidden debris `X`, for example `IA`
- `V`: vine blocker
- `T<id>`: target tile matching an entry in `targets`, for example `T0`

## Validation rules

The validator is pure .NET and operates on raw JSON strings. It checks:

- board width and height against the `tiles` array
- tile code recognition
- debris pool size and distribution consistency
- unique target ids
- target coordinates, matching `T<id>` board tiles, and stray board targets
- initial flooded rows, rise interval, dock size, and assistance chance ranges
- vine growth-priority bounds
- heuristic warnings for unreachable targets, disconnected dry regions, singleton-heavy dock traps, and water-budget pressure
- heuristic start errors when a target or one of its required access neighbors begins in flooded rows

## Loader behavior

- `Loader.LoadLevel(LevelJson, seed)` validates first, then builds the immutable `GameState`
- `Loader.LoadLevel(levelId, seed)` reads `Assets/Resources/Levels/<levelId>.json` through Unity `Resources`
- bottom `initialFloodedRows` are converted into `FloodedTile`
- `WaterState.ActionsUntilRise` starts at `riseInterval`
- `riseInterval = 0` means water is disabled for the level
