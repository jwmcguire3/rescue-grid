# Debug Panel

This panel is dev-only and only compiles when `UNITY_EDITOR` or `DEVELOPMENT_BUILD` is defined.

## Controls

- Tabs: `Play` keeps the existing step/replay/state inspection tools, and `Tune` hosts hot-reloadable level tuning plus preset management.
- Level selector: lists level ids discovered from `Assets/StreamingAssets/Levels`; selecting one reloads it with the current seed.
- Seed field: delayed integer field; committing a new value reloads the current level with that seed.
- Randomize Seed: generates a new seed, logs it to the Unity console, and reloads reproducibly.
- Play / Pause: auto-steps the board by repeatedly running the first valid action found on the current board.
- Step 1 Action: advances the simulation by exactly one pipeline action using the first valid tappable group.
- Speed: chooses the auto-step rate (`0.25x`, `0.5x`, `1x`, `2x`, `4x`).
- Fast Forward: forces auto-step to run at at least `4x`.
- Debug Undo: restores the previous debug snapshot from the panel's own stack. This is separate from the player-facing one-undo rule.
- Reset Level: restores the current level to its initial state for the current seed.
- Hazard readout: shows water `actionsUntilRise`, `riseInterval`, next flood row, vine `actionsSinceLastClear`, `growthThreshold`, and pending growth tile.
- Dock readout: shows occupancy, warning level, slot-order contents, `dockJamUsed`, and `dockJamEnabled`.
- RNG inspector: shows serialized RNG state as `S0:S1` and copies it to the clipboard.
- Spawn overrides: lets you switch assistance chance between current/`0`/`1`, choose force-emergency `Auto`/`On`/`Off`, and inspect `ConsecutiveEmergencySpawns` plus `SpawnRecoveryCounter`.
- Tune tab: hot-reloads the current level with overrideable `water.riseInterval`, `initialFloodedRows`, `assistanceChance`, force-emergency mode, dock jam enabled, debug-only dock size, default crate HP, and `vineGrowthThreshold`.
- Tune presets: saves and loads `TuningPresetAsset` ScriptableObjects from `Assets/Editor/TuningPresets/` so playtest setups can be replayed exactly.
- Instant Overflow Test: fills the dock with seven mismatched pieces, injects a two-piece group, and immediately runs the overflow action to exercise Dock Jam / overflow.
- Event log: keeps the last 20 actions, color-coded by event type. `DebugSpawnOverrideApplied` entries are tagged as dev-only telemetry.
- Copy State JSON: copies a bug-report wrapper containing level id, seed, timestamp, and the exported state.
- Copy Full GameState JSON: copies the full exported `GameState` shape, including `DebugSpawnOverride`.

## Key Bindings

- `F1`: toggle panel visibility
- `F2`: step one action
- `F3`: reset level
- `F4`: debug undo
- `Shift+F`: toggle fast-forward

## Runtime Notes

- The panel creates its `PanelSettings` at runtime and sets:
  - `scaleMode = ScaleWithScreenSize`
  - `referenceResolution = 1920x1080`
  - `screenMatchMode = MatchWidthOrHeight`
  - `match = 0.5`
  - `sortingOrder = 1000`
  - `clearColor = transparent`
- In the editor, the panel tries to load `DebugPanel.uxml` and `DebugPanel.uss`.
- In development players, it falls back to a code-built visual tree so the panel still works without scene plumbing.
