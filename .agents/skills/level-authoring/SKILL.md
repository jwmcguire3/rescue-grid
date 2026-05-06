---
name: level-authoring
description: Author, modify, validate, and review Rescue Grid level JSON files. Use when creating or editing levels, designing level sets, auditing difficulty or pacing, reviewing generated levels, checking density/readability, or confirming a board teaches the intended rescue, dock, water, blocker, or triage skill.
---

# Rescue Grid Level Authoring

## Purpose

Use this skill to create Rescue Grid levels that are valid, readable, fair, fun, and aligned with the game's identity.

A good Rescue Grid level is a rescue-order puzzle under calm urgency. It should not feel like tray sorting, generic cleanup, or an abstract match game with puppies attached.

Core identity:

1. Acting advances danger; thinking is free.
2. Dock tension feels self-authored.
3. Rescue order is the central puzzle.
4. Rescue extraction feels different from generic level completion.

## Read first

Before authoring or modifying levels, read the relevant sources:

- `docs/phase_1_spec.md` for authoritative Phase 1 design and rules.
- `docs/level-packets/phase1.packet.json` for current Phase 1 packet membership and packet policy.
- `docs/level-briefs/<levelId>.brief.json` for current per-level intent.
- `docs/phase_2a_plan.md` for active readability, animation/feedback, capture, and authoring-throughput scope.
- `Assets/Rescue.Content/README.md` for the Rescue.Content pipeline map.
- `Assets/Rescue.Content/AUTHORING.md` for validation, preview, template, and tooling commands.
- `references/LEVEL_DESIGN_PRINCIPLES.md` for rescue-first design rules, fairness, and common mistakes.
- `references/LEVEL_ROLES_AND_ARCHETYPES.md` when choosing the level role or reviewing pacing.
- `references/DENSITY_AND_READABILITY.md` when checking start-state density, first-move readability, empty cells, or required authoring discipline.
- `references/PHASE1_LEVEL_INTENTS.md` when modifying production onboarding levels covered by that reference.
- `references/FIRST_100_ROADMAP.md` when designing production campaign levels beyond the Phase 1 packet.
- `references/FIRST_100_PACING_MODEL.md` when designing production campaign flow, level-set sequencing, or retention/tension curves.
- `references/DIFFICULTY_TARGETS.md` when setting or reviewing difficulty, telemetry, fail-rate targets, bot outcomes, or tuning recommendations.

First-100 and other long-range references are north-star production/campaign guidance. They do not authorize new mechanics, new packet membership, or Phase 2A scope expansion unless the user explicitly asks for future-mechanic design or implementation.

Exact executable content behavior lives in `Assets/Rescue.Content/Schema.cs`, `Validator.cs`, `Loader.cs`, and `Tuning.cs`.

If a referenced file is missing, proceed from the current project docs and note the missing reference in the response.

## Required workflow

### 1. Identify the level brief

Before editing the grid, identify the matching brief in `docs/level-briefs/<levelId>.brief.json` and, for Phase 1 packet levels, confirm the level is expected by `docs/level-packets/phase1.packet.json`.

From the brief, identify:

- level id or range,
- level role,
- primary skill,
- secondary skill if any,
- board size,
- target count,
- allowed mechanics,
- intended tension beat,
- intended release beat,
- expected path,
- expected fail mode,
- density target.

If the user has not supplied these, infer reasonable defaults from the level range, Phase 1 intents, or roadmap. Ask only if the missing information would materially change the design.

After editing, verify brief-to-level conformance with the current `validate-brief` or `validate-brief-all` command from `Assets/Rescue.Content/AUTHORING.md`.

### 2. Respect current rules

Author only with supported tile codes and current mechanics.

Current core assumptions:

- valid groups are 2+ orthogonally adjacent same-type debris,
- dock size is 7 unless the rules explicitly change,
- dock insertion uses group remainder after triples,
- targets extract when required orthogonal neighbors are open,
- extraction must feel like the emotional endpoint,
- water advances by accepted actions, not real time,
- crates are route tax,
- ice is future value,
- vine is warned route pressure,
- spawn assistance must support recovery, not solve the level.

Do not rely on lucky spawn to prove the intended path.

### 3. Build around rescue

Place targets and rescue routes first.

Then add:

1. route blockers,
2. dock-relevant groups,
3. water/vine pressure,
4. tempting wrong moves,
5. surrounding board material,
6. empty cells only where justified.

Every level should have one dominant player-facing purpose.

### 4. Enforce density and readability

Use `references/DENSITY_AND_READABILITY.md` before finalizing the grid.

The opening board should look like real gameplay, not a tutorial diagram. Empty cells are allowed only for authored geometry, environmental negative space, hazard space, spawn corridors, deliberate rescue routes, or mobile readability.

If density is unusually low, justify it in `meta.notes`.

### 5. Write useful meta

Every level must have clear `meta`.

At minimum:

- `intent`: what the level is for,
- `expectedPath`: what good play does,
- `expectedFailMode`: the main fair mistake,
- `whatItProves`: why the level deserves to exist,
- `notes`: density, tuning, or design exceptions when needed.

Write meta so another designer can debug the level without asking you.

### 6. Validate, preview, and prove

Run the project validator after level JSON edits. Use the current commands in `Assets/Rescue.Content/AUTHORING.md`.

Typical commands:

```bash
./scripts/validate-levels.sh
./scripts/preview-level.sh L03
```

Validation passing is required but not sufficient. After editing levels, run or explicitly report why you did not run the relevant current commands:

- core and Phase 1 validation,
- brief conformance,
- ASCII preview, plus SVG preview when visual layout review matters,
- readability analysis,
- `design-report` for the edited level before calling it done,
- solve script verification,
- golden path verification for accepted packet levels,
- fail-path verification when a `.fail.json` file exists or the brief/review expects a fail path,
- assistance comparison before acceptance,
- review markdown generation or update.

For broad packet changes or playtest readiness, run the full authoring gate:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-level-authoring.ps1
```

For accepted manifest levels, a designer-approved golden path is required. Solver-found solves are useful proof, but they are not acceptance by themselves.

Expected fail paths should be verified wherever available. If a fail path is expected but missing, call that out as a review gap instead of treating the level as fully proven.

Update review markdown with `write-review` or `write-review-all`, or explain why review markdown was not applicable to the task.

### 7. Design-review before finalizing

Check:

- Does the board look visually complete?
- Are empty cells justified?
- Is the primary skill required?
- Is the first move readable?
- Is rescue order meaningful?
- Does dock pressure feel self-authored?
- Does water feel action-based, not timed?
- Is vine, if present, forecasted and attributable?
- Is the expected path playable without lucky spawn?
- Is the expected fail mode fair?
- Is there at least one satisfying move?
- Does the level end emotionally on rescue?
- Does this level fit the surrounding tension/release curve?

## Ask before acting if

Ask before proceeding only if:

- the request needs unsupported tile codes,
- the request changes rules or schema,
- the request changes dock size,
- the request needs a new mechanic,
- reinforced crates are required,
- the level cannot meet density and readability together,
- the expected path depends on hidden spawn luck,
- the level range has no roadmap and cannot be reasonably inferred.

Do not ask merely because the level number is beyond L15.

## Definition of done

A level or level set is valid only when:

- JSON validates,
- Phase 1 policy validation passes when the level belongs to the Phase 1 packet,
- brief-to-level conformance passes.

A level is reviewable only when:

- validation passes,
- preview is readable,
- readability analysis has been run,
- `design-report` has been run,
- density is acceptable or justified,
- the primary skill is clear,
- expected path is playable,
- expected fail mode is fair,
- loss attribution is clear,
- rescue progress is visible,
- dock pressure is self-authored,
- hazards are readable,
- the level has a satisfying move,
- the level fits pacing,
- meta explains the design clearly,
- review markdown is updated or the task clearly does not require it.

A Phase 1 packet level is accepted only when:

- it is expected by `docs/level-packets/phase1.packet.json`,
- solve verification passes,
- a designer-approved golden path exists and verifies,
- expected fail paths verify where available,
- assistance comparison has been reviewed,
- `--verify-acceptance` passes.

A packet is playtest-ready only when the full `scripts/verify-level-authoring.ps1` gate passes.
