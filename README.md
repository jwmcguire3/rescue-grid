# Rescue Grid

Rescue Grid is a Unity 6 Phase 1 prototype for a tactical animal-rescue puzzle game.

The prototype is scoped to prove the core seed:

- Acting advances danger; thinking is free.
- Dock pressure feels self-authored.
- Rescue order is the central puzzle.
- Extracting a puppy feels different from generic board completion.

The authoritative design source is `docs/phase_1_spec.md`. Do not pull mechanics from any other design documents unless the Phase 1 spec is deliberately updated.

## Current State

The repository is a playable Phase 1 prototype with a deterministic core, authored packet, debug scene, tooling, and first-pass presentation. The strongest areas are:

- Immutable `Rescue.Core` state and deterministic rules.
- A fixed action pipeline with isolated steps and regression tests.
- Authored L00-L15 level JSON content.
- Level validation, replay, solve-authoring, telemetry-report, and capture tooling.
- A functional Unity debug gameplay scene with board, dock, water, target, playback, debug, tuning, victory, and loss presentation.
- Phase 1 visual assets, prefabs, registries, and FX hooks for the debug presentation path.

The main product risk is player-facing clarity and emotional proof. A cold player should be able to read water pressure, dock failures, vine pressure, rescue order, and the puppy extraction beat without relying on debug context.

Latest checked-in test result artifacts show:

- EditMode: 463 passed, 0 failed (`editmode-results.xml`, run started 2026-04-27 07:00:00Z).
- PlayMode: 27 passed, 0 failed (`playmode-results.xml`, run started 2026-04-27 06:11:43Z).

## Phase 1 Scope

In scope:

- One hazard: water.
- Three blockers: crate, ice, vine.
- One target archetype: puppy.
- Fixed 7-slot dock.
- One free undo per level.
- Dock Jam as an early teaching variant for L01-L02.
- L00 rule-teach level.
- L01-L15 main packet.
- One-clear-away target state.
- Persistent next-flood-row forecast support.
- Authored vine growth priority and preview events.
- Seeded deterministic RNG.
- Basic spawn assistance and emergency spawn support.
- Extraction, win, loss, dock, water, blocker, vine, near-rescue, invalid-tap, and water-contact mode hooks/tests.
- Minimal capture path for the L15 ad/capture moment.

Out of scope for Phase 1:

- Fire, freeze fog, overgrowth, tools, keys, relics, switches, or resource pieces.
- Tool-gated rescues.
- Lucky drops.
- Power-ups beyond free undo.
- Variable dock sizes in normal play.
- Insertion preview.
- Full production treatment of the optional Distressed presentation state.
- Continuation offers.
- Shop, pass, cosmetics, economy, live ops, or sanctuary meta-loop.

Reinforced crate may exist as a data flag, but it should stay off by default unless Phase 1 tuning proves the core blocker trio is insufficient.

## Core Rules

`Assets/Rescue.Core/` has no Unity dependency. It owns immutable game state, rule helpers, seeded RNG, undo snapshots, and the action pipeline.

The valid-action pipeline currently runs:

1. `Step01_AcceptInput`
2. `Step02_RemoveGroup`
3. `Step03_DamageBlockers`
4. `Step04_ResolveBreaks`
5. `Step05_UpdateTargets`
6. `Step06_InsertDock`
7. `Step07_ClearDock`
8. `Step08_Extract`
9. `Step09_CheckWin`
10. `Step10_CheckLoss`
11. `Step11_Gravity`
12. `Step12_Spawn`
13. `Step13_TickHazards`
14. `Step14_ResolveHazards`
15. `Step15_CheckWaterConsequence`

Wins short-circuit after `Step09_CheckWin`, so a final rescue does not receive gravity, spawn, hazard, water, or same-action dock overflow failure. Dock overflow losses short-circuit after `Step10_CheckLoss`, before gravity, spawn, and hazards. Invalid input returns after `Step01_AcceptInput` and does not advance hazards.

Implemented gameplay behavior includes:

- Orthogonal same-type debris groups of size 2 or more are valid input.
- Singles, targets, blockers, ice, flooded tiles, frozen states, out-of-bounds taps, and empty tiles are rejected without hazard advancement.
- Removed debris enters the dock in group order.
- Dock triples clear after insertion, with compaction and warning state changes.
- Dock overflow and Dock Jam are represented in core events/outcomes.
- Gravity settles dry active pieces only.
- Spawns occur into dry space only and use deterministic RNG.
- Water rises by action threshold and floods whole rows from the bottom.
- L00 can pause water until the first valid action.
- Crates break from one adjacent clear.
- Ice breaks from one adjacent clear and reveals hidden debris.
- Vines break from one adjacent clear and grow from authored priority if ignored.
- Targets extract automatically when all required orthogonal neighbors are open.
- Target extractability latches immediately after blocker break resolution and cannot be undone by dock, gravity, spawn, water, or vine later in the action.
- One-clear-away, progressing, extractable, extracted, and optional Distressed target transitions are represented as real state/events in the shared immediate-loss / one-tick grace pipeline.
- Undo restores the exact previous snapshot for one action.

## Content

Authored level content lives in `Assets/StreamingAssets/Levels/`.

Current packet:

- `L00.json`: rule-teach opener.
- `L01.json` through `L15.json`: main Phase 1 packet.

Solve files live in `Assets/Resources/Levels/` as `L01.solve.json` through `L15.solve.json`. The L15 capture path is also represented in `capture/L15.capture.json` and documented in `docs/capture.md`.

The level schema currently supports:

- Level id/name and design metadata.
- Board width, height, and tile layout.
- Debris type pool and optional base distribution.
- Target coordinates.
- Initial flooded rows.
- Water rise interval and water-contact mode.
- Vine growth threshold and priority list.
- Dock size and Dock Jam flag.
- Assistance chance and consecutive emergency cap.
- Rule-teach metadata.

## Unity Implementation

The current Unity-facing project is a prototype/debug gameplay app for player-facing proof work.

Current scenes:

- `Assets/Scenes/Game.unity`
- `Assets/Scenes/DebugGameplay.unity`

`Game.unity` is the clean player/capture entry point. It boots L00 through the player-facing level session, progresses through L01-L15 from the victory screen, and keeps the debug panel out of the default player flow. `DebugGameplay.unity` remains the development scene for tuning, replay, and debug UI work.

Unity implementation areas:

- `Assets/Rescue.Unity/Board/`: board grid/content sync, water row display, water feedback, target feedback.
- `Assets/Rescue.Unity/UI/`: dock presenter.
- `Assets/Rescue.Unity/Input/`: board input presenter.
- `Assets/Rescue.Unity/Presentation/`: game state presenter, action playback builder/controller, win/loss screens.
- `Assets/Rescue.Unity/FX/`: core event to FX hook classification and sprite sequence playback.
- `Assets/Rescue.Unity/Debug/`: runtime debug/tuning panel for editor/development builds.
- `Assets/Rescue.Unity/Capture/`: L15 capture runner.
- `Assets/Rescue.Unity/Art/`: Phase 1 prefabs, textures, materials, registries, and validation helpers.
- `Assets/Rescue.Unity.EditorTools/`: editor-only diagnostics and prefab generation helpers.

Playback currently maps the main visible action events:

- group removal
- blocker break / ice reveal
- dock insert / clear / warning / jam / overflow feedback
- gravity
- spawn
- target extraction and near-rescue target feedback
- water rise and vine preview / growth feedback
- terminal win/loss
- final authoritative sync

Water forecast, Dock Jam, invalid-tap, vine, and target-readiness feedback have hooks; their final readability depends on presentation tuning.

## Telemetry and Debugging

`Assets/Rescue.Telemetry/` defines telemetry event records and JSON conversion for:

- level start, win, and loss
- dock occupancy
- water rise, water-contact mode, and distressed target outcomes
- vine growth
- undo use
- target extraction/loss
- invalid taps
- idle time and time to first action
- hazard proximity
- Dock Jam trigger/resolution
- capture snapshots
- tuning changes
- action taken with RNG before/after state

`Assets/Rescue.Unity/Debug/` provides a development-only panel with:

- level and seed selection
- step, auto-play, speed, fast-forward, reset, and debug undo
- hazard, dock, RNG, and spawn override readouts
- hot tuning overrides and tuning presets
- instant overflow test
- event log
- state JSON export

## Tooling

Important scripts:

```bash
scripts/test.sh
scripts/validate-levels.sh
scripts/watch-levels.sh
scripts/replay.sh
scripts/preview-level.sh
scripts/telemetry-report.sh
scripts/build-capture.sh
scripts/record-l15.sh
scripts/build-web.sh
scripts/build-android.sh
scripts/build-ios.sh
```

Windows test wrapper:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms EditMode
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms PlayMode
```

Tool projects:

- `Tools/LevelValidator/`: validates authored level JSON.
- `Tools/Replay/`: replays deterministic trajectories.
- `Tools/SolveAuthoring/`: searches and replays authored solves.
- `Tools/TelemetryReport/`: summarizes telemetry output.

Capture verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-capture.ps1
```

## Repository Map

- `docs/phase_1_spec.md`: authoritative Phase 1 design and playtest contract.
- `docs/tuning.md`: tuning notes.
- `docs/distribution.md`: distribution notes.
- `docs/capture.md`: L15 capture build and recording workflow.
- `Assets/Rescue.Core/`: immutable state, rules, pipeline, RNG, undo.
- `Assets/Rescue.Core.Tests/`: core EditMode tests.
- `Assets/Rescue.Content/`: level schema, loader, validator, ASCII preview.
- `Assets/Rescue.Content.Tests/`: content tests.
- `Assets/StreamingAssets/Levels/`: authored L00-L15 level JSON.
- `Assets/Resources/Levels/`: authored solve files.
- `Assets/Rescue.Replay/`: replay runtime code.
- `Assets/Rescue.Telemetry/`: telemetry schema and logger/hooks.
- `Assets/Rescue.Unity/`: Unity presentation, debug UI, capture, art integration.
- `Assets/Rescue.Unity.EditorTools/`: editor-only tools.
- `Assets/Tests/EditMode/`: Unity-facing EditMode tests.
- `Assets/Tests/PlayMode/`: PlayMode smoke/debug tests.
- `Tools/`: .NET CLI support tools.
- `scripts/`: local automation.

## Known Gaps

The next work should make existing Phase 1 behavior clearer within the locked scope.

Highest-value gaps:

- Strengthen `TargetOneClearAway`, `WaterWarning`, `VinePreviewChanged`, and `VineGrown` as player-facing presentation.
- Make persistent next-flood-row forecast visually unmistakable.
- Make dock overflow, Dock Jam, win, and loss causality clearer.
- Tune invalid-tap reject bump/audio while preserving zero state change.
- Strengthen target extraction so it reads as a rescue beat.
- Add minimal Mae reaction and aftercare support.
- Add or promote the eventual main `Game.unity` scene when the debug gameplay scene graduates.
- Verify L00-L15 as a player-facing progression, not just as deterministic content.

## Playtest Lens

Judge Phase 1 by player interpretation, not feature count.

Good player language:

- "I picked the wrong rescue first."
- "I overfilled the dock."
- "I had time to think."
- "I needed to save the lower puppy first."

Bad player language:

- "The timer got me."
- "I got unlucky."
- "It is just a sorter with puppies."
- "I knew the answer, but the game did not let me do it."

Core questions:

- Why did you lose that level?
- Did the water feel fair or annoying?
- Did you ever feel rushed while thinking?
- What was the dock asking you to pay attention to?
- Did saving the puppy feel different from just finishing a level?
- What, if anything, felt random?
- When vine grew, did it feel warned or arbitrary?
- Would you describe this as a rescue game or as a sorting game?

For the rest of Phase 1, prefer clarity over breadth. If this scoped version does not read as Rescue Grid, adding more systems will only hide the answer.
