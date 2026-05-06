# Rescue Grid — Agent Instructions

## Project
Rescue Grid is a Phase 1-complete prototype puzzle game now focused on Phase
2A readability, animation/feedback, authoring throughput, and capture proof.
The implemented gameplay rules authority remains at /docs/phase_1_spec.md.
Read it before planning implementation work. Do not implement mechanics outside
that spec unless the task explicitly asks for future-mechanic design or
implementation. Phase 2A docs may guide readability, animation/feedback,
capture, and authoring workflow; full-game, v3.2, and first-100 docs are
north-star references, not default implementation authority.

Core purpose: prove that acting advances danger, thinking is free, and
rescue order is the central puzzle.

## Language and tooling
- C# 10+ with nullable reference types enabled globally.
- Unity 6.4 
- Test runner: Unity Test Framework (NUnit).
  - EditMode tests for anything in Rescue.Core and Rescue.Content.
  - PlayMode tests only for integration and smoke tests that need the
    Unity runtime.
- CLI test wrappers:
  - Windows: `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms EditMode`
  - Windows PlayMode: `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms PlayMode`
  - Bash wrapper: `scripts/test.sh` forwards to `scripts/test.ps1`.
- Unity compiler language version is pinned via `Assets/csc.rsp`; keep it
  at C# 10+ unless the project is deliberately migrated and verified.
- `Assets/csc.rsp` also carries the global nullable setting and any
  compiler-wide language settings required by Core.
- `ImmutableArray<T>` support is provided by the checked-in
  `Assets/Plugins/System.Collections.Immutable.dll` plugin; do not remove
  it unless Unity package/runtime support is replaced intentionally.
- Formatting and analyzer preferences are defined in the root `.editorconfig`.
  Treat it as the repository style source; do not assume analyzer warnings are
  warnings-as-errors unless a script, project, or CI configuration explicitly
  wires that behavior.

## Source-of-truth locations
- Repo-wide agent instruction authority: `AGENTS.md`.
- Implemented gameplay rules authority: `docs/phase_1_spec.md`.
- Active Phase 2A scope authority: `docs/phase_2a_plan.md`.
- Phase 1 packet membership and packet policy authority:
  `docs/level-packets/phase1.packet.json`.
- Per-level intent authority: `docs/level-briefs/`.
- Main playable/player scene: `Assets/Scenes/Game.unity`.
- Debug/testing/playback scene: `Assets/Scenes/DebugGameplay.unity`.
- Authored playable level JSON: `Assets/StreamingAssets/Levels/`.
- Authored solve/replay JSON: `Assets/Resources/Levels/`.
- Level-authoring workflow/gate authority:
  `Assets/Rescue.Content/AUTHORING.md`.
- Content pipeline map: `Assets/Rescue.Content/README.md`.
- Codex level-authoring workflow/router:
  `.agents/skills/level-authoring/SKILL.md`.
- Scene membership authority: `ProjectSettings/EditorBuildSettings.asset` plus
  the files present under `Assets/Scenes/`.

## Architecture rules (non-negotiable)

1. Rescue.Core has ZERO Unity dependencies. No `using UnityEngine`, no
   MonoBehaviour, no coroutines, no Time.deltaTime, no Debug.Log, no
   UnityEngine.Random. All randomness goes through the seeded Rng in
   Rescue.Core/Rng/. If you catch yourself wanting to add a Unity
   dependency to Core, stop — that logic belongs in Rescue.Unity.

2. The action pipeline in Rescue.Core/Pipeline/ executes in the exact
   12-step order defined in /docs/phase_1_spec.md section 1.4. Do not
   reorder. Do not collapse steps.

3. Every state type in Rescue.Core is immutable:
   - Use `readonly record struct` for small value types.
   - Use `ImmutableArray<T>` for collections (not `List<T>` or arrays).
   - Use `record` classes for larger states (GameState, Board).
   - Records with `with` expressions produce new instances for modifications.
   - No public setters anywhere in Core.

4. Nullable reference types are enabled. Non-null is the default. An
   explicit `?` is required for nullable. Do not suppress nullability
   warnings with `!` — fix the design instead.

5. Rescue.Unity reads Rescue.Core state. It never mutates Core state.
   MonoBehaviours that display state hold a reference to a GameState
   snapshot and re-render on change; they do not call back into Core
   mutations except through the pipeline driver.

6. Every rule in the spec maps to exactly one place in code. If two files
   decide the same rule, that is a bug — fix it, don't work around it.

## Unity-specific rules

- ScriptableObjects for tunable configs (level defaults, spawn weights)
  where hot-reload is useful. NEVER use ScriptableObjects for runtime game
  state.
- MonoBehaviours are thin adapters only: route input into the pipeline,
  observe events and drive animations. No game logic inside MonoBehaviour
  update methods.
- No `Time.time`, `Time.deltaTime`, `Time.frameCount` in Rescue.Core.
  If timing matters (it shouldn't, since the pipeline is action-driven,
  not time-driven), that timing belongs in Rescue.Unity.
- Scenes: `Game.unity` is the main playable/player scene.
  `DebugGameplay.unity` is the existing debug/testing/playback scene. Levels
  are data in `Assets/StreamingAssets/Levels/`, not scenes.
- Do not edit, rename, or reorganize scenes, prefabs, art assets, or level JSON
  unless the current task explicitly scopes that work.
- Meta files: commit .meta files. Never delete them manually. If you
  rename or move a script, use Unity's rename, not a raw file system
  rename — Unity regenerates GUIDs otherwise and references break.
- Serialization: do not rely on Unity's built-in JsonUtility for level
  loading — it's too limited for the schema. Use System.Text.Json
  (bundled in .NET Standard 2.1) or Newtonsoft.Json (via the Unity
  package manager).

## Testing rules

- When changing pipeline behavior, keep or add focused EditMode coverage for
  the affected step.
- When changing deterministic rules, keep or add a determinism test: given a
  seed and an input sequence, repeated runs must produce the same final state.
- PlayMode tests are for Unity integration and smoke coverage that needs the
  runtime.
- Core tests do NOT create GameObjects or reference UnityEngine.
  If a Core test needs a MonoBehaviour, the design is wrong.
- Do not treat the current L03 Phase 1 validator warning as a blocker unless a
  task explicitly asks to retune L03; it is an accepted content-policy warning.

### Unity test execution

- Use the repository test wrapper for routine Unity validation:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms EditMode`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Platforms PlayMode`
- In Codex desktop, run the PlayMode wrapper with escalated permissions rather
  than from the default sandbox. Unity 6000.4.3f1 has repeatedly hit a Windows
  breakpoint dialog before emitting `playmode-results.xml` or
  `playmode-tests.log` from the sandbox, while the same wrapper succeeds
  outside it.
- Do not invoke `Unity.exe` directly for routine validation unless explicitly
  instructed by the user. Avoid manual Unity commands such as
  `Unity.exe -batchmode -nographics -projectPath ... -runTests ...`, including
  focused `-testFilter` launches.
- Unity may show a Windows breakpoint dialog when launched from a sandboxed or
  restricted shell:
  - `Unity.exe - Application Error`
  - `The exception Breakpoint ... (0x80000003)`
- If the breakpoint dialog appears, do not keep retrying the same sandboxed or
  manual Unity command. Check whether a result XML and Unity log were produced.
- If no result XML or Unity log exists, report `Unity launch failed before tests
  started` and treat it as an environment/sandbox issue, not a gameplay, code,
  or test failure.
- When reporting Unity validation, distinguish between tests passed, tests
  failed with result XML/log evidence, Unity launch failed before tests started,
  and environment/sandbox issues.

## Agent workflow
- Read `docs/phase_1_spec.md` before planning implementation work.
- Prefer the implementation for repository paths and scene names; prefer
  `docs/phase_1_spec.md` for gameplay rules.
- Keep documentation edits targeted. Do not rewrite broad sections when a small
  correction is enough.
- Do not perform broad refactors or cleanup while making scoped fixes.

## Phase 2A Scope Guard

Phase 1 is closed as an engineering/prototype milestone unless current tests,
authored levels, APK/device playability, or core progression break. Active
Phase 2A work focuses on readability, animation/feedback, level-authoring
tools, and capture proof; it must not expand mechanics.

Do not add new mechanics, meta systems, monetization, shop/economy, new hazards,
new blockers, District 2, or broad art/UI replacement unless explicitly
requested. Do not continue broad refactors just because files are large; tie
refactors to a concrete bug, feature, test-backed seam, or maintainability
blocker.

After Unity-facing Phase 2A changes, use the documented EditMode and PlayMode
test wrappers. If Unity shows a breakpoint launch dialog and no result XML/log
exists, report it as an environment launch failure, not a gameplay or test
failure.

## Refactor boundary

The Phase 1 safe-refactor pass is complete. It already extracted passive helper
logic from the major presenter/debug files, including
`BoardContentViewPresenter`, `DockViewPresenter`, and `DebugPanel`.

Future refactors must be tied to a specific bug, feature, test-backed seam, or
maintainability blocker. Do not continue broad refactoring just because a file
is large.

Do not casually refactor behavior-sensitive areas:
- gameplay pipeline order
- replay stepping and playback flow
- animation coroutines and timing/easing logic
- dock warning, jam, overflow, and triple-clear behavior
- level loading/reset/next-level flow
- hotkey and callback wiring
- scene presenter resolution
- telemetry/session mutation
- authored level JSON and solve/replay files

After any future refactor touching Unity-facing code, run both EditMode and
PlayMode through the repository test wrappers.

## Commit style
- One logical change per commit. Reference the task ID in the message
  (e.g. "A1: implement SeededRng with mulberry32").
- Do not commit Library/, Temp/, Logs/, Build/, or obj/. The .gitignore
  in the repo handles this — do not disable it.
- PR descriptions must include: what rule/system this implements, which
  spec section it maps to, and which tests cover it.

## What to ask before acting
- If a rule in /docs/phase_1_spec.md is ambiguous, ask. Do not pick an
  interpretation silently.
- If a task requires touching files outside its stated scope, ask.
- If a test is flaky, surface it. Do not retry until it passes.
- If you find yourself wanting to add a Unity dependency to Rescue.Core,
  stop and ask — the design has drifted.
