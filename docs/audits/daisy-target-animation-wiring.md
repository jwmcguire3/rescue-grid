# Daisy Target Animation Wiring Validation

Date: 2026-05-14

## Asset Paths

- Daisy target prefab: `Assets/Rescue.Unity/Art/Prefabs/Targets/PF_Target_Daisy_Puppy.prefab`
- Daisy animator controller: `Assets/Rescue.Unity/Art/Animation/Targets/Daisy/AC_Daisy_Target.controller`
- Daisy material: `Assets/Rescue.Unity/Art/Materials/Targets/M_Daisy_Repainted.mat`
- Phase 1 target registry: `Assets/Rescue.Unity/Art/Registries/Phase1TargetVisualRegistry.asset`

## Clip Mapping

- `Trapped` and `Distressed`: `Target_Trapped_Idle`
- `Progressing`: `Target_Progress_Fidget`, falling back to `Target_Progress_Idle`
- `OneClearAway`: `Target_OneClearAway_Bark`, falling back to `Target_OneClearAway_Idle`
- Extraction: `Target_Extract_Start`, falling back to `Target_Extract_Air`

## Registry And Scene Wiring

- `Phase1TargetVisualRegistry` has both `puppyPrefab` and `fallbackTargetPrefab` wired to `PF_Target_Daisy_Puppy`.
- `Assets/Scenes/Game.unity` and `Assets/Scenes/DebugGameplay.unity` both serialize `BoardContentViewPresenter.targetRegistry` to `Phase1TargetVisualRegistry`.
- PlayMode smoke coverage verifies live targets in both scenes render with `TargetPuppyAnimator`, use `AC_Daisy_Target`, keep root motion disabled, have valid renderer materials, and do not drift across the tile during an idle animator update.

## Test Results

- EditMode: passed via `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms EditMode`.
- PlayMode: passed via `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms PlayMode`.
- Note: the first sandboxed EditMode launch produced no Unity log; rerunning the wrapper outside the sandbox succeeded.

## Remaining Manual Unity Import Steps

- None observed. Daisy prefab, controller, material, and registry references are imported and test-covered.
