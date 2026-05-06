# Vine Growth Planning Decision Memo

## Status

Decision memo for the next vine-growth implementation tasks. This document does not implement the planner. Gameplay authority remains `docs/phase_1_spec.md`; Phase 2A scope remains readability, feedback, authoring throughput, and capture proof.

The desired implementation direction is hybrid:

- systemic rescue-pressure scoring by default
- optional authored priority, bias, and constraints for designer control
- deterministic behavior
- legacy compatibility for current levels and artifacts

`docs/audits/vine-growth-behavior-audit.md` was requested as context but is not present in this workspace.

## Current Behavior Summary

Current runtime vine state is `VineState` in `Assets/Rescue.Core/State/Types.cs`:

- `ActionsSinceLastClear`
- `GrowthThreshold`
- `GrowthPriorityList`
- `PriorityCursor`
- `PendingGrowthTile`

Current JSON fields are in `Assets/Rescue.Content/Schema.cs`:

- `vine.growthThreshold`
- `vine.growthPriority`, an ordered array of `{ row, col }`

The loader copies `vine.growthPriority` directly into `VineState.GrowthPriorityList` and starts `PriorityCursor` at `0`. There are no JSON fields today for systemic goals, bias weights, frontier rules, or path constraints.

Current `growthPriority` behavior:

- During Step 11 hazard ticking, if no vine was cleared this action, the counter increments.
- When `ActionsSinceLastClear >= GrowthThreshold - 1` and `PendingGrowthTile` is null, `Step11_TickHazards` scans `GrowthPriorityList` from `PriorityCursor` forward.
- The first coordinate passing `VineGrowthTiles.IsValidGrowthTile` becomes `PendingGrowthTile`.
- Valid growth tiles are in-bounds `EmptyTile`, `DebrisTile`, or unlatched `RescuePathTile`.
- A future empty priority coordinate is reserved against gravity/spawn by `IsReservedFutureGrowthTile`.
- If a coordinate is invalid at preview selection time, the scan skips it.

`PendingGrowthTile` is selected one action before growth, at threshold - 1, if a valid priority coordinate exists. It is not recalculated while pending unless the plan is cleared or invalidated by hazard resolution.

`VinePreviewChanged` fires in Step 11 when a non-null `PendingGrowthTile` is first selected. Current event payload is the pending tile. Presentation uses this event and/or state to show the warned preview.

`VineGrown` fires in Step 12 when `VineGrowthPending` is true, `PendingGrowthTile` is still valid, and that tile is replaced with a `BlockerTile(BlockerType.Vine, 1, Hidden: null)`. On successful growth:

- `ActionsSinceLastClear` resets to `0`
- `PendingGrowthTile` resets to null
- `PriorityCursor` advances to the entry after the grown coordinate when the coordinate came from the authored list
- `SpawnLineageByCoord` removes the grown coordinate

If the pending tile is invalid by Step 12, no vine grows and `PendingGrowthTile` resets to null. The counter is not reset by this invalidation today.

Clearing vine resets growth in two places:

- Step 4 marks `VineClearedThisAction` and resets `ActionsSinceLastClear` to `0` when a vine blocker breaks.
- Step 11 sees `VineClearedThisAction`, keeps the counter at `0`, clears `PendingGrowthTile`, and suppresses preview/growth for that action.

The current behavior is deterministic because it uses fixed state, fixed pipeline order, fixed list scan order, and no random source. It is overly authored because levels must name the exact future growth tiles. The system does not choose a rescue-relevant goal, does not choose a source/frontier vine, and does not path one step toward pressure; designer-authored order is doing most of the planning.

## Product Target

Vine should pressure rescue-relevant routes instead of acting like arbitrary board noise.

Target behavior:

- pressure actual and likely rescue routes
- plan a tile before the final warning action, so warning reads as a committed plan rather than a last-moment lookup
- grow one valid step from an existing vine/frontier toward a rescue-pressure goal
- stay fully deterministic
- preserve existing Phase 1 levels where possible

This is a refinement of the current Phase 1 vine behavior, not a new hazard or mechanic.

## New Behavior: First Implementation Version

### Candidate Rescue-Pressure Goals

The planner should build a deterministic candidate goal set from the current state:

- unextracted and unlatched target required-neighbor cells
- existing `RescuePathTile` cells for unextracted/unlatched targets
- blocked required-neighbor cells adjacent to unextracted/unlatched targets
- cells on shortest reachable routes from existing vine frontiers toward those required-neighbor cells, if needed for pathing

Exclude goals for extracted targets and extractable-latched targets. Latched rescues should remain protected by the existing pipeline contract: once a target latches, later vine growth cannot undo that extraction.

Whether `OneClearAway`, `Progressing`, and `Trapped` targets should receive different goal weights needs confirmation in Task 2/3.

### Source And Frontier Selection

The planner should derive source/frontier candidates from current vine blockers on the board.

A source vine is a current `BlockerTile` with `BlockerType.Vine`. Its frontier candidates are orthogonal neighboring tiles that pass `VineGrowthTiles.IsValidGrowthTile`.

If there are no current vine blockers, systemic planning has no source. In that case, use legacy authored priority fallback if available. If neither systemic source nor authored fallback exists, no plan is created.

Whether authored `growthPriority` may nominate a virtual source when no vine exists needs confirmation in Task 2/3. Default for the first version should be no virtual source.

### Pathing Rules

Pathing should be grid-based, orthogonal, deterministic, and board-state based.

Allowed path traversal for planning:

- `EmptyTile`
- `DebrisTile`
- unlatched `RescuePathTile`

Blocked for path traversal:

- targets
- flooded tiles
- crates
- ice
- existing vines except the selected source
- latched/extracted rescue paths where applicable
- out-of-bounds cells

The grown tile must always be a currently valid growth tile. The planner may path through multiple valid cells, but each growth event places exactly one vine tile: the first step from the selected source/frontier toward the selected goal.

Whether pathing should consider gravity/spawn future occupancy before the final warning needs confirmation in Task 2/3. First version should use the current post-spawn board at Step 11 planning time.

### Next-Growth-Tile Selection

First version should choose:

1. a rescue-pressure goal
2. a vine source/frontier path toward that goal
3. the next growth tile as the first valid tile on that path adjacent to the source/frontier

The next growth tile should be stored as `PlannedGrowthTile` and mirrored to `PendingGrowthTile` when compatibility requires existing preview/growth behavior.

Planning should happen before the threshold - 1 warning if possible. Proposed first version:

- when the counter enters a growth cycle with no plan, compute and store a plan as soon as there is enough board state after an accepted action
- at threshold - 1, emit existing `VinePreviewChanged(PlannedGrowthTile)` if the plan is still valid
- at threshold, grow the planned tile if still valid

The exact action count at which early planning begins needs confirmation in Task 2/3. The minimum acceptable version is to plan no later than threshold - 2, so the warning action is not also choosing the plan.

### Scoring And Tie-Break Order

The planner must produce identical output for identical state. Do not use RNG.

Recommended scoring order:

1. Prefer goals belonging to unextracted/unlatched targets under higher rescue pressure.
2. Prefer goals that block or tax a required rescue neighbor over generic nearby debris.
3. Prefer shorter path length from current vine frontier to goal.
4. Prefer authored `growthPriority` or future bias fields when they apply.
5. Prefer lower water safety margin targets, if water pressure is already represented in state. Needs confirmation in Task 2/3.
6. Tie-break by target order in `GameState.Targets`.
7. Tie-break by path/source row, then col.
8. Tie-break by next-growth-tile row, then col.

For the first implementation, keep scoring simple and inspectable:

- target relevance score
- path distance
- authored priority index/bias
- row/col tie-breaks

Do not add opaque weights that require tuning infrastructure unless Task 2/3 confirms a concrete need.

### Authored Priority Compatibility

Existing `vine.growthPriority` remains supported.

First-version compatibility rules:

- If systemic candidates exist, use systemic planning by default.
- Authored priority can bias systemic selection by ranking matching goals, sources, or next-step tiles earlier.
- If the authored list contains the exact systemic next step, prefer the lower authored index among otherwise comparable candidates.
- If systemic pathing fails, fall back to the current authored priority scan from `PriorityCursor`.
- If a level has authored priority but no viable systemic source/frontier, fall back to authored priority.
- Preserve `PriorityCursor` for legacy fallback and for authored-list compatibility.

Future JSON fields for explicit priority mode, bias weights, or constraints are deferred. Any schema addition needs a separate implementation task and content migration plan.

### Invalidation Behavior

A plan can become invalid because the board changed after planning:

- the planned tile becomes a target, blocker, flooded tile, or latched/extracted rescue path
- the planned tile moves out of valid growth status
- the source vine is cleared
- the target goal extracts or latches
- no valid path remains from any vine frontier to the goal

First-version invalidation:

- If vine is cleared, cancel the plan immediately and reset the counter as current behavior does.
- If the planned tile is invalid before preview, recompute once in Step 11.
- If the planned tile is invalid at threshold resolution, recompute only if doing so would still preserve warning fairness. Default: do not grow an unpreviewed replacement on the same action.
- If invalidation happens after preview but before growth, emit `VinePreviewChanged(null)` only if presentation needs an explicit clear. Current presentation can also clear from state. Needs confirmation in Task 2/3.

Do not silently grow a different tile than the warned tile on the threshold action.

### Clearing And Cancel Behavior

Clearing any vine should:

- reset `ActionsSinceLastClear` or the replacement cycle counter to `0`
- clear `PlannedGrowthTile`
- clear `GrowthSourceTile`
- clear `GrowthGoalTile`
- clear `PendingGrowthTile`
- suppress preview and growth for that action

This preserves the current player promise: cutting vine interrupts the current pressure plan.

## State Model

Minimal proposed `VineState` fields:

- `ActionsSinceLastClear`
- `GrowthThreshold`
- `GrowthPriorityList`
- `PriorityCursor`
- `PendingGrowthTile`
- `PlannedGrowthTile`
- `GrowthSourceTile`
- `GrowthGoalTile`

`ActionsIntoGrowthCycle` is a possible replacement name for `ActionsSinceLastClear`, but it is not required now. Reusing `ActionsSinceLastClear` is lower risk because tests, telemetry, debug UI, and smoke harnesses already know it.

Required now for first implementation:

- `PlannedGrowthTile`: committed systemic tile, selected before warning.
- `GrowthSourceTile`: the vine tile or frontier source used to explain/path the plan.
- `GrowthGoalTile`: rescue-pressure goal the plan is walking toward.
- `PendingGrowthTile`: keep as compatibility field for existing preview rendering, events, tests, replay, and telemetry.

Deferred:

- authored mode enum such as `Systemic`, `AuthoredOnly`, `SystemicWithAuthoredBias`
- authored constraint regions or lanes
- weighted JSON scoring parameters
- multi-step path cache
- full `ActionsIntoGrowthCycle` rename/migration

Compatibility behavior:

- `PendingGrowthTile` should mirror `PlannedGrowthTile` once the warning is active.
- Existing consumers should continue using `PendingGrowthTile` until presentation and telemetry are deliberately migrated.
- If fallback uses legacy authored priority, `PlannedGrowthTile`, `GrowthSourceTile`, and `GrowthGoalTile` may remain null while `PendingGrowthTile` carries the legacy coordinate. Alternatively, set `PlannedGrowthTile` to the fallback coordinate and leave source/goal null. The latter is preferable for consistency; needs confirmation in Task 2/3.

## Events

Preserve existing events:

- `VinePreviewChanged(TileCoord? PendingTile)`
- `VineGrown(TileCoord Coord)`

These remain the presentation and replay contract for Phase 2A.

New event candidates:

- `VineGrowthPlanned(TileCoord plannedTile, TileCoord? sourceTile, TileCoord? goalTile)`
- `VineGrowthProgressed(TileCoord sourceTile, TileCoord nextTile, TileCoord goalTile)`
- `VineGrowthCanceled(TileCoord? plannedTile, string reason)`

Needed now:

- `VineGrowthPlanned`: not required for first implementation unless telemetry/debug UI needs to display the earlier plan before preview.
- `VineGrowthProgressed`: not required now. `VineGrown` already marks the visible growth tile.
- `VineGrowthCanceled`: not required now if cancellation is fully represented by state plus `VinePreviewChanged(null)` where needed.

Recommendation: defer new public events for the first implementation. Add internal planner result data and keep the external event surface stable. Add new events only when a concrete presentation, telemetry, or debugging task needs them.

## Compatibility Mode

Existing levels should behave as follows:

- If systemic candidates exist, use systemic planning. Authored `growthPriority` biases/tie-breaks but does not have to name every tile.
- If only authored priority exists, use the existing authored scan behavior from `PriorityCursor`.
- If systemic pathing fails, fall back to authored priority.
- If both systemic and authored priority fail, no pending preview is created and no vine grows.
- If `growthThreshold` is `999` with no priority, treat it as effectively no active growth for current packet purposes. Do not create systemic growth unless Task 2/3 explicitly decides that threshold alone means active systemic vine.
- If `growthThreshold` is `999` but systemic candidates exist, default first-version behavior should still suppress practical growth because those levels are authored as static/no-growth vine beats. Needs confirmation in Task 2/3 before implementation.

Legacy artifact expectations:

- L08/L13/L15 current tests expect preview and growth when vine is ignored. They should still preview and grow, though exact tile may change under systemic mode.
- Golden/solve/fail files that rely on specific vine outcomes may need updates if systemic mode changes board evolution.
- Authored fallback should allow current levels to remain playable even before content retuning.

## Test Plan For Implementation Tasks

Required EditMode coverage:

- planned tile is selected before threshold - 1
- preview at threshold - 1 uses the previously planned tile
- growth targets a rescue-relevant tile or a path step toward one
- growth chooses one valid step from an existing vine frontier
- deterministic tie-breaking for equal-scoring targets, paths, and next steps
- authored priority biases or wins the documented tie-break
- clearing any vine cancels plan and resets the counter
- clearing vine suppresses same-action preview/growth
- invalid planned tile before preview triggers documented recompute/fallback
- invalid planned tile after preview does not grow an unpreviewed replacement
- no valid systemic candidate produces no preview/growth
- no vine source falls back to authored priority when available
- legacy authored priority fallback preserves current list/cursor behavior
- threshold `999` / no-growth levels do not unexpectedly activate
- undo restores plan fields, counter, cursor, and pending tile
- repeated runs from the same seed and input sequence produce the same final vine state
- pipeline order remains Step 11 plan/preview tick, Step 12 growth resolution

Unity-facing tests are not required for the planner itself unless new events or state fields are consumed by presentation.

Replay/golden validation should be run after implementation because vine growth can alter board state, solve viability, and fail attribution.

## Artifact Risk

Most likely affected levels:

- `L08`: tutorial currently depends on one authored center preview/growth tile.
- `L13`: vine pressure exam currently uses an authored lane; fail notes say the current fail branch does not produce a clean `VineGrown` before loss.
- `L14`: late stress test has authored vine pressure in a denser multi-target board.
- `L15`: capture/hero level has committed solve and capture proof; any vine change risks recorded proof stability.
- `L18`: dock discipline exam has light authored route pressure with long threshold.
- `L20`: dock discipline capstone has authored route pressure in a full board.

Likely affected artifacts:

- `Assets/Resources/Levels/*.solve.json` for any vine level whose exact board evolution changes
- `Assets/Resources/Levels/*.golden.json` for accepted packet paths
- `Assets/Resources/Levels/*.fail.json` for vine, dock, and water fail attribution
- capture documentation and reports tied to `L15`
- EditMode tests in `Assets/Tests/EditMode/Content/VineGrowthPipelineTests.cs`
- replay and smoke tests that construct `VineState` directly
- telemetry/debug surfaces that print `ActionsSinceLastClear`, `PriorityCursor`, or `PendingGrowthTile`

## Open Decisions

Needs confirmation in Task 2/3:

- exact cycle moment for first planning: cycle start, threshold - 2, or first post-action state with valid candidates
- whether water urgency should be part of rescue-pressure scoring in version one
- whether target readiness states should carry different weights
- whether authored priority is only bias/fallback or can hard-constrain the candidate set
- whether `growthThreshold: 999` should permanently suppress systemic growth for static-vine levels
- whether invalidation after preview emits `VinePreviewChanged(null)` or relies on state sync
- whether legacy fallback should populate `PlannedGrowthTile` when source/goal are null
- whether no-source authored priority can still grow disconnected vines as legacy compatibility
