# Vine Encroachment Capture Review

Date: 2026-05-06

## Scope

Reviewed planned vine encroachment presentation for capture readiness before cold tester footage. This was a minimal audit pass only: no Core rules, level JSON, prefabs, materials, or tuning values were changed.

Gameplay authority checked first: `docs/phase_1_spec.md`, especially the vine requirement that growth must feel warned, deterministic, and attributable.

## Levels Reviewed

- `L08` - Vine Growth Tutorial / practice read.
- `L13` - Vine Pressure Exam.
- `L18` - Dock Discipline Exam with light vine pressure.
- `L20` - Dock Discipline Capstone with vine under broader rescue/dock pressure.

Reviewed artifacts and implementation paths:

- `Assets/StreamingAssets/Levels/L08.json`
- `Assets/StreamingAssets/Levels/L13.json`
- `Assets/StreamingAssets/Levels/L18.json`
- `Assets/StreamingAssets/Levels/L20.json`
- `docs/level-briefs/L08.brief.json`
- `docs/level-briefs/L13.brief.json`
- `docs/level-briefs/L18.brief.json`
- `docs/level-briefs/L20.brief.json`
- `Reports/L08-audit-preview.svg`
- `Reports/LevelTelemetry/L08.telemetry.json`
- `Reports/LevelTelemetry/L13.telemetry.json`
- `Reports/LevelTelemetry/L18.telemetry.json`
- `Reports/LevelTelemetry/L20.telemetry.json`
- `Assets/Rescue.Unity/Board/BoardContentViewPresenter.cs`
- `Assets/Rescue.Unity/Presentation/ActionPlaybackController.cs`
- `Assets/Rescue.Unity/Feedback/Phase1AudioFeedbackRegistry.asset`
- Related EditMode/PlayMode tests around vine overlay, playback, FX, and audio routing.

## Clips / Screenshots

No L08/L13/L18/L20 motion clips were found in the workspace.

Existing media/artifacts found:

- `Reports/L08-audit-preview.svg` - static L08 layout preview.
- `Build/Logs/L15.capture.json` - existing automated capture report, but it is L15-only and does not exercise vine encroachment.
- `Build/Android/device-screen*.png` - existing Android screenshots, not scoped to this vine review.

Important capture gap: the current capture runner and docs are still centered on `-capture-l15` and `Assets/Resources/Levels/L15.solve.json`. That is useful for the hero capture path, but it does not produce a vine motion proof for the requested review levels.

## Review Answers

1. Can the viewer identify the planned growth tile before takeover?

Likely yes in implementation, needs motion proof. `BoardContentViewPresenter` now renders a `VineGrowthPreview` from `PlannedGrowthTile` before `PendingGrowthTile`, including over empty, debris, and rescue-path tiles. L08 and L13 have authored priority lanes that should make the destination tile attributable. Without video, the remaining unknown is whether the quiet planned state is visible enough on a busy board.

2. Can the viewer see progress across actions?

Likely yes in implementation, needs motion proof. Planned overlay coverage derives from `ActionsSinceLastClear / GrowthThreshold` and animates toward the new coverage after action sync. This matches "acting advances danger; thinking is free." The main uncertainty is perceptual: L13/L18/L20 have denser boards, so the small per-action scale change may be too subtle in capture.

3. Can the viewer distinguish calm encroachment, one-action warning, final takeover, and vine clear/reset?

Code path says mostly yes:

- Calm encroachment: planned-only overlay uses lower coverage/alpha.
- One-action warning: `VinePreviewChanged` uses the stronger preview pulse/rest scale.
- Final takeover: `VineGrown` uses takeover color and full-scale growth before final sync.
- Clear/reset: state sync clears the overlay when plan/pending fields clear or when the tile becomes a real vine.

The weakest distinction to verify in motion is calm encroachment vs one-action warning. Both reuse the same overlay object and palette, so the distinction depends on scale, alpha, and pulse timing reading clearly in context.

4. Does the animation steal attention from rescue target readability?

Probably not in L08. Risk is moderate in L13 because vine is the exam pressure and sits on the route lane near rescue priorities. Risk is low-to-moderate in L18/L20 because vine is intentionally secondary; a too-bright warning could pull attention away from dock/rescue order. No evidence supports visual tuning yet, because the missing artifact is actual motion capture.

5. Does takeover feel warned, not arbitrary?

Likely yes for authored lanes. L08 grows into the center gap above the target, L13 has a visible vertical lane, and L18/L20 use limited priority lists. The implementation also keeps the planned overlay visible before pending warning, which addresses the previous "sudden preview only" risk.

6. Does FX/SFX feel too noisy?

Likely acceptable from routing/registry inspection, needs audio capture. Vine preview and final growth have separate feedback ids, `VinePreview` volume is lower than `VineGrow`, and both are capped at one play per route. FX routing uses the vine preview FX for both preview and growth, which is economical but may make final takeover feel like a louder repeat of the same beat rather than a distinct event.

7. Does final sync ever pop or overwrite the animation?

Covered by existing tests. `ActionPlaybackController` schedules `VineGrowth` before `FinalSync`, and `ActionPlaybackController_VineGrowthTakeoverRunsBeforeFinalSyncReplacesOverlay` verifies the overlay is present and near full scale at final sync before the real vine blocker replaces it. This does not prove perceptual smoothness in a recorded player build, but the ordering risk appears controlled.

## Issues Found

### P1 - No vine-specific motion capture artifact exists

The requested review is specifically about reading the mechanic in motion, but the workspace currently has no L08/L13/L18/L20 clip. Static layout and code review are enough to say the implementation is structurally ready, not enough to sign off for cold testers.

Impact: do not tune serialized visuals yet from capture alone, because there is no capture of the requested mechanic.

### P2 - Capture automation is L15-only

The documented capture runner is hard-wired around `-capture-l15`, `L15.solve.json`, and `L15.capture.json`. That blocks repeatable vine proof for L08/L13/L18/L20 without manual play or new capture plumbing.

Impact: manual recording is still possible, but repeatability and audit trail are weaker than the existing L15 proof path.

### P3 - Calm vs warning distinction is the main perceptual risk

The current implementation reuses the same overlay object/color family for planned encroachment and pending warning, with scale/alpha/pulse doing the differentiation. That is a reasonable minimal approach, but it is the first thing to inspect in motion, especially on L13 and L20.

Impact: possible tiny serialized-material/timing follow-up if capture shows the calm state is invisible or the warning state is too similar.

## Decision

Recommendation: defer visual tuning until a short vine-specific motion capture exists.

No-change for code and content right now. The implementation has the expected presentation pieces, routed feedback, and final-sync coverage. The missing proof is not a rules or level issue; it is a capture workflow gap.

Do not tune L08/L13/L18/L20 from this review alone.

## Narrow Follow-Up Task

Add a repeatable vine capture path for one practice clip and one pressure clip, without changing Core rules or level JSON.

Suggested scope:

- Extend the existing capture workflow to accept a level id plus solve resource, or add explicit `-capture-l08-vine` / `-capture-l13-vine` commands.
- Use `L08` for the practice read and `L13` for the pressure read.
- Output reports under `Build/Logs/` or the existing capture output folder with level-specific names.
- Record one short clip per level and update this audit with the clip paths plus timestamped observations for planned, pending, takeover, clear/reset, and final sync.

Only after those clips exist should any tiny polish be considered, limited to serialized timing/material/overlay values.

## Validation

No EditMode or PlayMode wrappers were run because this pass made no runtime code, level, prefab, material, or serialized tuning changes. Existing relevant tests identified during review include:

- `BoardContentViewPresenter_RendersPlannedVineOverlayBeforePendingPreview`
- `BoardContentViewPresenter_PlannedVineOverlayCoverageFollowsActionProgress`
- `BoardContentViewPresenter_PendingVinePreviewReadsStrongerThanEarlyProgress`
- `BoardContentViewPresenter_AnimateVineGrowthCreatesOverlayUntilFinalSyncReplacesIt`
- `ActionPlaybackController_VineGrowthTakeoverRunsBeforeFinalSyncReplacesOverlay`
- `Phase1AudioFeedbackRegistry_VinePreviewIsQuieterThanFinalGrowth`
- `ActionPlaybackSmokeTests.VinePreviewAndGrowthSmokeShowsAuthoredTileThenGrowth`
