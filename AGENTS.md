# Rescue Grid — Agent Instructions

## Project
Phase 1 prototype of a puzzle game. The authoritative design spec is at
/docs/phase_1_spec.md. Read it before planning any task. Do not reference
or pull mechanics from any other design document.

Core purpose: prove that acting advances danger, thinking is free, and
rescue order is the central puzzle.

## Language and tooling
- C# 10+ with nullable reference types enabled globally.
- Unity 6.4 
- Test runner: Unity Test Framework (NUnit).
  - EditMode tests for anything in Rescue.Core and Rescue.Content.
  - PlayMode tests only for integration and smoke tests that need the
    Unity runtime.
- CLI test command: `Unity -batchmode -runTests -testPlatform EditMode -projectPath .`
  (wrapped in scripts/test.sh for convenience).
- Unity compiler language version is pinned via `Assets/csc.rsp`; keep it
  at C# 10+ unless the project is deliberately migrated and verified.
- `Assets/csc.rsp` also carries the global nullable setting and any
  compiler-wide language settings required by Core.
- `ImmutableArray<T>` support is provided by the checked-in
  `Assets/Plugins/System.Collections.Immutable.dll` plugin; do not remove
  it unless Unity package/runtime support is replaced intentionally.
- Lint: Roslyn analyzers via .editorconfig. Treat warnings as errors in CI.

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
- Scenes: one main scene `Game.unity` for play, one `DebugSandbox.unity`
  for isolated debug work. Levels are data (JSON in StreamingAssets), not
  scenes.
- Meta files: commit .meta files. Never delete them manually. If you
  rename or move a script, use Unity's rename, not a raw file system
  rename — Unity regenerates GUIDs otherwise and references break.
- Serialization: do not rely on Unity's built-in JsonUtility for level
  loading — it's too limited for the schema. Use System.Text.Json
  (bundled in .NET Standard 2.1) or Newtonsoft.Json (via the Unity
  package manager).

## Testing rules

- Every pipeline step has an EditMode unit test that exercises it in
  isolation.
- Every rule has a determinism test: given a seed and an input sequence,
  the final state must be byte-identical on repeated runs.
- Smoke tests (PlayMode) must pass headless: load level → take 3 actions
  → undo → retry → complete.
- Core tests do NOT create GameObjects or reference UnityEngine.
  If a Core test needs a MonoBehaviour, the design is wrong.

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
