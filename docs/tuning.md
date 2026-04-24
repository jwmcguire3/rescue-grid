# Tuning

The Tune tab in the dev debug panel is a playtest-only control surface for changing authored balance without editing level JSON. Each change hot-reloads the current level through `Loader.LoadLevel(...)` and preserves the current seed unless the seed field itself changes.

## Tunables

- `water.riseInterval`: Actions between water rises. Use this first when the level feels too timer-like or too slack.
- `initialFloodedRows`: Starting waterline. Prefer increasing this before making water faster when you want earlier urgency.
- `assistanceChance`: Base strength of the spawn helper. Lower it to expose harsher boards; raise it to recover readability and fairness.
- `force emergency assistance`: Debug override for the emergency-assist gate. Use `On` to stress-test recovery behavior and `Off` to verify that a board still reads without emergency help.
- `dock jam enabled`: Enables or disables the one-time Dock Jam teaching safeguard.
- `dock size (debug)`: Experimental debug-only override for exploring dock pressure. This is not authoritative Phase 1 content and should not be copied back into level schema as design truth.
- `default crate HP`: Changes the HP assigned to normal `CR` crate tiles during load. This does not rewrite authored reinforced crates.
- `vineGrowthThreshold`: Actions between vine growth checks when vine is not cleared.

## Presets

Tune presets are stored as `TuningPresetAsset` ScriptableObjects under `Assets/Editor/TuningPresets/`. Saving a preset captures the current override set; loading a preset reapplies those overrides and hot-reloads the active level.

## Workflow

Observe the failure or pressure point first.
Form one hypothesis about what is causing it.
Change one variable.
Replay with the same seed so the comparison stays auditable.
Revert or save the preset once the result is understood.

Recommended loop: observe -> hypothesize -> change one variable -> test -> revert or commit.
