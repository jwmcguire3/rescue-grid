# Rescue Grid

Rescue Grid is a Phase 1 Unity prototype for a warm, tactical animal-rescue puzzle game.

The current build is focused on proving the core game seed:

- acting advances danger; thinking is free
- dock tension feels self-authored
- rescue order is the central puzzle
- extracting the puppy feels different from generic board completion

This repository is not trying to implement the full commercial v3.2 game yet. It is a scoped Phase 1 prototype with water, dock pressure, puppy extraction, undo, crate / ice / vine blockers, level validation, replay tooling, telemetry hooks, and a first pass of Unity presentation.

## Current implementation state

The project is currently close to Phase 1 mechanically. The strongest completed areas are the core rules pipeline, level content structure, deterministic replay/validation tooling, and the first presentation/playback layer.

The main remaining Phase 1 work is not broad feature expansion. It is player-facing readability and emotional proof: making sure a cold player can tell why they won or lost, why water advanced, why the dock overflowed, why vine growth was fair, and why saving the puppy feels like a rescue rather than a generic clear.

## Phase 1 scope

### In scope

Phase 1 keeps only the systems needed to prove the seed:

- one hazard: water
- three blockers: crate, ice, vine
- one target archetype: puppy
- 7-slot dock
- one free undo per level
- Dock Jam only as an early teaching variant
- Level 0 rule-teach
- 15 main packet levels plus L00
- one-clear-away target state
- persistent next-flood-row forecast target
- one real extraction beat
- minimal Mae / aftercare support
- basic telemetry and replay support

### Out of scope for Phase 1

Do not treat these as missing work yet:

- fire
- overgrowth
- freeze fog
- tools
- keys
- relics
- tool-gated rescues
- lucky drops
- power-ups beyond free undo
- distinct dock sizes
- insertion preview
- distressed-state soft recovery
- continuation offers
- shop, pass, cosmetics, economy, live ops
- sanctuary meta loop
- full v3.2 character / district / monetization systems

Reinforced crate may exist as a data flag, but it should stay off by default unless late Phase 1 tuning proves the core blocker trio is insufficient.

## Core rules pipeline

The core action pipeline is intentionally rigid. If this order becomes fuzzy, fairness becomes fuzzy.

Current valid-action order:

1. accept input
2. remove tapped group
3. damage adjacent blockers
4. resolve broken blockers / ice reveals
5. insert removed group into dock
6. clear dock triples
7. apply gravity
8. spawn new pieces
9. extract targets
10. check win
11. tick hazards
12. resolve hazards
13. check loss

Important fairness rule: target extraction happens before hazard advance. A target saved on the last move should be saved, and a win should not receive a post-win hazard step.

## Implemented systems

### Core gameplay

- Orthogonal same-type debris groups are valid input.
- Single tiles and invalid targets do not advance hazards.
- Groups are removed immediately before dock insertion.
- Dock accepts pieces left-to-right.
- Dock clears triples after insertion.
- Dock warning states exist for safe / caution / acute / overflow pressure.
- Dock Jam exists as an early teaching affordance.
- Gravity and spawn resolve after dock processing.
- Undo restores the previous authoritative snapshot.
- Win and loss outcomes are represented in the action result.

### Hazards

Water is the only Phase 1 hazard.

- Water advances by player action threshold, not by real time.
- Idle thinking does not advance water.
- Water rises by row.
- Flooded rows become inactive.
- If water reaches an unrescued target, the level is lost.
- L00 can pause water until the first valid action for rule-teach purposes.

### Blockers

Current blocker set:

- crate: one adjacent clear breaks it
- ice: one adjacent clear breaks it and reveals hidden debris
- vine: one adjacent clear breaks it; if ignored, it advances on its own counter

Vine growth is authored through a priority list rather than random spread. The core emits preview/growth events, but those events still need stronger visual presentation.

### Targets

- Puppy targets occupy board tiles.
- Targets are not movable and not tappable.
- A target extracts automatically when all required orthogonal neighbors are open.
- Edge and corner targets use only existing orthogonal neighbors.
- One-clear-away target state exists in state/events.
- Target extraction has a first-pass animation, but the rescue beat still needs more emotional weight.

### Content

The project includes a Phase 1 level packet:

- `L00` rule-teach
- `L01`–`L15` main packet levels
- level JSON under `Assets/StreamingAssets/Levels/`
- solve files under `Assets/Resources/Levels/`
- capture support for the L15 ad/capture moment

The content schema supports:

- board size
- tile layout
- debris pool
- target list
- initial flooded rows
- water rise interval
- vine growth priority
- dock size and Dock Jam flag
- assistance chance
- level intent / expected path / expected fail mode metadata

## Presentation state

The Unity presentation layer is functional but still Phase 1 placeholder-quality.

Already present:

- board content sync
- debris, blocker, hidden-debris, and target visual registries
- action playback builder
- action playback controller
- final authoritative sync after playback
- cancel/recover path that re-syncs to authoritative state
- dock feedback hooks
- water-rise animation hook
- blocker damage/break animation hooks
- ice reveal animation hook
- gravity/spawn movement hooks
- target extraction animation hook

Known presentation gaps:

- `TargetOneClearAway` needs clearer visible/debug feedback.
- `WaterWarning` needs player-facing treatment.
- Persistent next-flood-row forecast needs visual confirmation or stronger implementation.
- `VinePreviewChanged` and `VineGrown` need explicit animation/FX treatment.
- `DockOverflowTriggered`, `Lost`, and `Won` need clearer player-facing causality.
- `InvalidInput` should produce a small reject bump/audio without state change.
- Target extraction needs to feel more like a rescue beat.
- Mae / aftercare is still the largest affective gap.

## Recommended next work

Do not start by adding v3.2 breadth. The next work should make the existing Phase 1 state readable.

Recommended order:

1. Map ignored but Phase-1-critical events into lightweight presentation:
   - `TargetOneClearAway`
   - `WaterWarning`
   - persistent next-flood-row forecast
   - `VinePreviewChanged`
   - `VineGrown`
   - `DockOverflowTriggered`
   - `Lost`
   - `Won`
   - `InvalidInput`
2. Strengthen target extraction so it reads as rescue, not generic disappearance.
3. Add minimal Mae reaction and aftercare beat.
4. Verify L00–L15 in play mode for player-facing causality.
5. Run playtests around the Phase 1 hypotheses.
6. Use telemetry and player language to tune water, dock, assistance, and level readability.

## Playtest questions

Phase 1 should be judged by player interpretation, not feature count.

Ask players:

- Why did you lose that level?
- Did the water feel fair or annoying?
- Did you ever feel rushed while thinking?
- What was the dock asking you to pay attention to?
- Did saving the puppy feel different from just finishing a level?
- What, if anything, felt random?
- When vine grew, did it feel warned or arbitrary?
- Would you describe this as a rescue game or a sorting game?

Good signs:

- “I picked the wrong rescue first.”
- “I overfilled the dock.”
- “I had time to think.”
- “I needed to save the lower puppy first.”

Bad signs:

- “The timer got me.”
- “I got unlucky.”
- “It’s just a sorter with puppies.”
- “I knew the answer, but the game didn’t let me do it.”

## Repository map

Key areas:

- `Assets/Rescue.Core/` — immutable game state, rules, pipeline, RNG, undo
- `Assets/Rescue.Core/Pipeline/` — action result, events, and step order
- `Assets/Rescue.Content/` — level JSON schema, loader, validator
- `Assets/StreamingAssets/Levels/` — Phase 1 level definitions
- `Assets/Resources/Levels/` — solve files / authored solve support
- `Assets/Rescue.Unity/` — Unity presentation, board view, dock view, debug UI, capture hooks
- `Assets/Rescue.Unity/Presentation/` — action playback builder/controller and state presenter
- `Assets/Rescue.Unity/Board/` — board content and target feedback presenters
- `Assets/Rescue.Telemetry/` — telemetry schema/hooks
- `Assets/Rescue.Replay/` and `Tools/Replay/` — replay tooling
- `Tools/LevelValidator/` — content validation tooling
- `docs/phase_1_spec.md` — Phase 1 design and playtest contract

## Development principle

For the rest of Phase 1, prefer clarity over breadth.

If the current scoped version does not read as Rescue Grid, adding more systems will only hide the answer. The correct next step is to make the existing seed legible, testable, and emotionally distinct.