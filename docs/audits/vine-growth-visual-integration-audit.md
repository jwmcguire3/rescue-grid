# Vine Growth Visual Integration Audit

## Scope

This audit identifies where Phase 2A planned vine visuals should integrate. It does not change Core rules, level JSON, solve/golden/fail artifacts, prefabs, materials, or runtime visuals.

Gameplay authority remains `docs/phase_1_spec.md`. Phase 2A only authorizes readability and feedback for the existing spreading-vine pressure system.

## Current State And Events

`VineState` in `Assets/Rescue.Core/State/Types.cs` currently exposes:

- `ActionsSinceLastClear`
- `GrowthThreshold`
- `GrowthPriorityList`
- `PriorityCursor`
- `PendingGrowthTile`
- `PlannedGrowthTile`
- `GrowthSourceTile`
- `GrowthGoalTile`

`GameState` does not carry a separate normalized growth-progress field. Progress should be derived in presentation from:

```text
progress = clamp01(ActionsSinceLastClear / GrowthThreshold)
```

For the warned/takeover window, use `PendingGrowthTile != null` as the compatibility signal that the plan is now player-visible. `PlannedGrowthTile` may exist earlier, before the threshold-1 warning.

Only these public Core events exist today:

- `VinePreviewChanged(TileCoord? PendingTile)`
- `VineGrown(TileCoord Coord)`

These candidate events are mentioned in planning docs but are not present in `Assets/Rescue.Core/Pipeline/Events.cs`:

- `VineGrowthPlanned`
- `VineGrowthProgressed`
- `VineGrowthCanceled`

Current firing behavior:

- Plan chosen: `Step11_TickHazards` computes `PlannedGrowthTile`, `GrowthSourceTile`, and `GrowthGoalTile` when no valid plan exists. No event fires.
- Progress advances: `Step11_TickHazards` increments `ActionsSinceLastClear` when no vine was cleared. No event fires.
- Preview threshold reached: when `ActionsSinceLastClear >= GrowthThreshold - 1`, `PendingGrowthTile` mirrors the planned tile and `VinePreviewChanged(pendingTile)` fires.
- Growth completes: when `ActionsSinceLastClear >= GrowthThreshold`, `Step12_ResolveHazards` turns `PendingGrowthTile` into a vine blocker and fires `VineGrown(coord)`.
- Growth canceled/reset: clearing any vine in `Step04_ResolveBreaks` and `Step11_TickHazards` clears pending/planned/source/goal and resets the counter. No cancel event fires. Invalid plans are cleared in `Step11_TickHazards`; invalid pending growth is cleared in `Step12_ResolveHazards`; neither path emits a clear event unless a `VinePreviewChanged(null)` is explicitly produced elsewhere.

## Current Unity Presentation Path

`ActionPlaybackBuilder` maps:

- `VinePreviewChanged` -> `ActionPlaybackStepType.VinePreview`
- `VineGrown` -> `ActionPlaybackStepType.VineGrowth`

`ActionPlaybackController` then calls:

- `BoardContentViewPresenter.AnimateVinePreview`
- `BoardContentViewPresenter.AnimateVineGrowth`

`FxEventRouter` also routes both events to `VineGrowPreviewFx`, but this is a one-shot FX layer. It should remain supplemental and should not own persistent planned-growth state.

## Where Tile Visuals Are Created And Synced

Persistent board content is owned by `BoardContentViewPresenter`.

Existing responsibilities there:

- `SyncImmediate(GameState)` reconciles debris, blockers, hidden debris, targets, rescue path markers, and the vine preview.
- `ReconcileTileContent(...)` creates blocker visuals for actual `BlockerTile(BlockerType.Vine, ...)`.
- `SyncVinePreview(GameState)` reads `state.Vine.PendingGrowthTile` and creates/clears the current preview object.
- `AnimateVinePreview(...)` creates/pulses the preview object.
- `AnimateVineGrowth(...)` scales the preview object to full tile coverage before final sync.

The planned vine overlay should live in `BoardContentViewPresenter`, not `FxEventRouter`, because it must persist across player actions, be reconciled from `GameState`, survive immediate sync/debug playback, and clear deterministically when state clears.

## Overlay Support

`BoardContentViewPresenter` already supports a vine overlay object on `EmptyTile` via `SyncVinePreview`, and the Phase 1 rules allow vine growth onto `EmptyTile`, `DebrisTile`, and unlatched `RescuePathTile`.

Current limitation: `SyncVinePreview` only renders when `PendingGrowthTile` is an `EmptyTile`. `AnimateVinePreview` can spawn at any anchored coord, but immediate sync will clear it unless the tile is empty.

Recommendation:

- Extend the vine overlay eligibility check to match Core growth validity for presentation: empty, debris, and unlatched rescue-path tiles.
- Do not duplicate full Core rules in Unity. Add a small presentation helper that accepts those display-safe tile types and relies on Core for real validity.
- Keep the overlay in its own object, outside debris/blocker/rescue-path registries, so it can layer over debris and rescue-path markers without changing their state.

## Prefab And Material Fit

Current persistent overlay prefab:

- `Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Vine_Overlay_Phase1.prefab`

Current final vine prefab:

- `Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Vine.prefab`

The overlay prefab is a simple mesh with `Vine_Overlay_Phase1.mat`. `BoardContentViewPresenter` already scales the overlay object and applies tint through renderers/graphics. This supports scale animation now.

Alpha animation is partly supported by presenter utilities such as `SetVisualAlpha`, but the current overlay material is opaque Standard shader settings (`_Mode: 0`, `_ZWrite: 1`). Renderer tint alpha may not produce reliable transparency until the material/prefab is configured for transparent rendering. Treat alpha fade as a material/prefab follow-up, not an assumption.

## Recommended Overlay Representation

Use one persistent `VineGrowthPreview` object for the planned/current growth tile, backed by `BlockerVisualRegistry.VineOverlayPrefab` with fallback to the vine blocker prefab.

Recommended visual states:

- Planned but not pending: very low coverage/quiet encroachment on `PlannedGrowthTile`, derived from cycle progress.
- Pending/warned: current `VinePreviewChanged` pulse on `PendingGrowthTile`.
- Growth/takeover: `VineGrown` scales the overlay to full coverage, then final sync replaces it with the actual vine blocker visual.
- Goal/source direction: if `GrowthSourceTile` and/or `GrowthGoalTile` exist, orient or bias the overlay toward the source-to-goal vector. Do this inside the presenter without adding Core events.

Use `GrowthGoalTile` as direction/pressure context only; do not create a second gameplay marker by default. If a later test needs an explicit goal indicator, add it as a clearly visual-only child of the preview object.

## Progress Calculation

Recommended calculation:

```text
if GrowthThreshold <= 0: progress = 0
else progress = clamp01(ActionsSinceLastClear / GrowthThreshold)
```

Map progress conservatively:

- Planned-only coverage can start near `0.20` and grow toward the existing rest scale.
- When `PendingGrowthTile` is set, preserve existing `VinePreviewRestScale`/pulse behavior for compatibility.
- When `VineGrown` fires, use existing full-scale growth animation and one-shot FX/haptic/audio routes.

Do not use wall-clock time to advance progress. It should update only when the board syncs after actions, preserving "acting advances danger, thinking is free."

## Final Sync Risk

Final board sync does replace the growth overlay with the real blocker. This is intentional and already covered by tests. The risk is timing: `ActionPlaybackController` waits for the `VineGrowth` step duration before final sync, so the takeover animation has the configured `VineGrowthDurationSeconds` window. If playback is disabled or duration is zero, final sync replaces it immediately.

Implementation should keep the final sync behavior, but ensure any longer encroachment animation happens before `VineGrown` from state sync/progress, not by delaying authoritative final sync after growth.

## Existing Test Coverage

Core/tests protecting planning and compatibility:

- `Assets/Rescue.Core.Tests/Rules/VineTests.cs`
- `Assets/Rescue.Core.Tests/Rules/VineGrowthPlannerTests.cs`
- `Assets/Tests/EditMode/Content/VineGrowthPipelineTests.cs`
- `Assets/Rescue.Core.Tests/Determinism/PipelineDeterminismTests.cs`
- `Assets/Rescue.Core.Tests/Undo/UndoFlowTests.cs`

Unity/tests protecting presentation/playback/routing:

- `Assets/Tests/EditMode/Rescue.Unity/Board/BoardContentViewPresenterTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Presentation/ActionPlaybackBuilderTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Presentation/ActionPlaybackControllerTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/FX/FxEventRouterTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Feedback/FeedbackEventClassifierTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Feedback/AudioEventRouterTests.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Haptics/HapticEventRouterTests.cs`
- `Assets/Tests/PlayMode/Smoke/ActionPlaybackSmokeTests.cs`

## Recommended Files To Touch Next

Primary:

- `Assets/Rescue.Unity/Board/BoardContentViewPresenter.cs`
- `Assets/Tests/EditMode/Rescue.Unity/Board/BoardContentViewPresenterTests.cs`

Possible but only if needed:

- `Assets/Rescue.Unity/Presentation/ActionPlaybackSettings.cs` for tunable planned encroachment scale/duration.
- `Assets/Rescue.Unity/Art/Materials/Phase1/Vine_Overlay_Phase1.mat` if alpha fade must be reliable.
- `Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Vine_Overlay_Phase1.prefab` if a direction child/arrow is required.

Do not touch Core rules/events for the first visual pass.

## Likely Tests To Add

- `BoardContentViewPresenter` renders quiet planned overlay from `PlannedGrowthTile` before `PendingGrowthTile` is set.
- Planned overlay coverage increases when `ActionsSinceLastClear` increases, without wall-clock time.
- Planned overlay renders over `DebrisTile`.
- Planned overlay renders over unlatched `RescuePathTile`.
- Planned overlay clears when planned/pending fields clear.
- Pending preview still uses `VinePreviewChanged` and existing registered overlay prefab.
- `VineGrown` still leaves a full overlay until final sync replaces it with `Blocker_Vine`.
- Direction/goal cue, if implemented, orients from `GrowthSourceTile` toward `GrowthGoalTile` without requiring new Core events.

## Implementation Risks

- Duplicating Core validity in Unity could drift. Keep Unity eligibility display-only and small.
- Overlay over debris/rescue-path may z-fight because current y offset is tuned for empty-tile preview. Verify layering in PlayMode.
- Alpha fade may not work with the current opaque material.
- A single `vinePreviewObject` is enough for Phase 1's one-growth-tile cap; future multi-growth would need a registry, but that is out of scope.
- `VinePreviewChanged(null)` is not the main cancellation contract today. State sync must clear overlays reliably.
- Playback-disabled or zero-duration modes will not show takeover linger; tests already expect immediate final replacement.

## Exact Next Implementation Task

Implement a Unity-only planned vine overlay pass in `BoardContentViewPresenter`:

1. Extend vine preview sync to use `PlannedGrowthTile` before `PendingGrowthTile`.
2. Derive coverage from `ActionsSinceLastClear / GrowthThreshold`.
3. Allow the overlay to render over empty, debris, and unlatched rescue-path tiles.
4. Preserve existing `VinePreviewChanged` pulse and `VineGrown` takeover behavior.
5. Add focused EditMode presenter tests for planned-only, pending, debris, rescue-path, clear/reset, and final-sync replacement.

No Core rule, event, level, solve, golden, fail, prefab, or material change is required for that first task.
