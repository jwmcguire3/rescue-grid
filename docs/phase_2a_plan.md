# Phase 2A Plan - Readability, Animation, and Production Pipeline

Phase 2A turns the complete Phase 1 engineering/prototype milestone into a readable, emotionally legible vertical-slice base without expanding mechanics.

Phase 1 is closed unless current tests, authored levels, APK/device playability, or core progression break. Phase 2A does not mean the game is production-ready; it means the next proof target has moved from rules existence to player interpretation.

## Purpose

Turn the Phase 1 prototype into something a cold player can understand, feel, and explain while playing it, not just after reading an explainer.

Core question:

Can a cold player understand, feel, and explain the game while playing it, not just after reading an explainer?

## Goals

1. Make rescue feel different from clearing.
   - Target interpretation: "I opened the path and saved the puppy."
   - Avoid: "The board cleared something."

2. Make dock failure feel self-caused.
   - Target interpretation: "I overfilled the dock because I cleared the wrong group."
   - Avoid: "The game randomly ended."

3. Make water pressure feel warned and action-based.
   - Target interpretation: "That row was about to flood. I had time to think, but my move advanced it."
   - Avoid: "The timer got me."

4. Make vine growth feel warned, not arbitrary.
   - Target interpretation: "I saw that vine was about to grow."
   - Avoid: "Where did that come from?"

5. Improve level-authoring throughput.
   - Target: author, verify, tune, replay, and inspect levels quickly without fragile manual steps.

6. Produce one capture-quality proof moment.
   - Target: a 10-15 second clip that clearly shows rescue-path pressure, dock pressure, water danger, and a satisfying rescue.

## Priorities

1. Rescue extraction beat.
2. Dock causality.
3. Water forecast and rise.
4. Vine preview and growth.
5. Level-authoring tools.
6. Capture proof.

## Exit Criteria

Technical:

- EditMode passes.
- PlayMode passes.
- Level validation passes.
- Solve verification passes.
- APK works on device.
- L00-L15 still play in order.
- No new mechanics added outside approved scope.

Player readability, measured with 3-5 cold players:

- At least 3 can explain that the goal is to open rescue paths around the puppy.
- At least 3 can explain that thinking is free but actions advance danger.
- At least 3 can explain why a dock failure happened.
- At least 3 can identify water/vine warning before or immediately after seeing it once.
- At least 3 describe the game as a rescue puzzle, not just a sorter.

Presentation:

- Puppy extraction has a distinct animation/feedback beat.
- Dock insertion, triple-clear, caution, danger, Jam/overflow states are visually distinct.
- Water forecast and water rise are clearly different states.
- Vine preview and vine growth are clearly different states.
- Invalid taps feel rejected but not punishing.
- Mae/aftercare support exists only where it improves clarity or emotional grounding.

Production pipeline:

- Level authoring loop is faster and less fragile.
- Capture workflow can reliably produce one clean L15 or capture-level clip.
- README / phase docs reflect the Phase 2A boundary.
- AGENTS.md protects against scope expansion and broad refactors.

## Operating Rules

1. No new mechanics until the current loop reads.
2. Animate causality first.
3. Small changes, tested often.
4. Cold-player confusion beats internal taste.
5. Codex tasks must stay narrow.

## Out Of Scope

- New hazards.
- New blockers.
- New animal species, except a visual-only swap for a specific capture test if explicitly requested.
- District 2.
- Sanctuary meta-loop.
- Shop.
- Economy.
- Cosmetics.
- Live ops.
- Remote config.
- IAP.
- Rewarded ads.
- Continuation offers.
- Power-ups.
- Tools.
- Keys.
- Relics.
- Switches.
- Resource pieces.
- Insertion preview.
- Major UI redesign.
- Broad architecture refactors.
- Broad art replacement.
- Production-grade animation system.
- Full Mae character system.
- Full distressed-state production treatment.
- "Make everything look final."

## First 10 Tasks

### 1. Phase 2A Docs Lock

Goal: Make the active phase and scope boundary unambiguous.

What to do: Add this plan, update the README if needed, and add an AGENTS.md scope guard.

Acceptance criteria: The repo docs say Phase 1 is closed as an engineering/prototype milestone, Phase 2A is active, and Phase 2A does not expand mechanics.

### 2. Baseline Readability Capture

Goal: Record the current player-facing readability before changing presentation.

What to do: Capture a short L15 or capture-level run and note where rescue, dock, water, and vine causality read poorly.

Acceptance criteria: A baseline clip or capture notes exist, with timestamped readability issues and no gameplay changes.

### 3. Rescue Extraction Animation Design Spec

Goal: Define the first distinct rescue beat before implementation.

What to do: Specify timing, visual emphasis, audio/haptic intent, Mae/aftercare involvement, and how the beat differs from clearing.

Acceptance criteria: The spec identifies the minimum rescue beat and the events/states it listens to without changing rules.

### 4. Implement First Rescue Extraction Beat

Goal: Make saving a puppy read as rescue, not generic board cleanup.

What to do: Implement the smallest Unity-facing animation/feedback pass for extraction using existing rules/events.

Acceptance criteria: Extraction is visually distinct, final rescue still wins immediately, and EditMode/PlayMode pass.

### 5. Dock Causality Audit

Goal: Identify why dock failure may read as random or delayed.

What to do: Review dock insertion, triple-clear, caution, danger, Jam, overflow, loss explanation, and playback order in motion.

Acceptance criteria: A short audit lists the top dock causality gaps and the first improvement to make.

### 6. Implement First Dock Causality Improvement

Goal: Make the player's chosen group visibly cause the dock state change.

What to do: Improve one dock feedback beat, such as insertion path, occupancy warning, triple-clear, Jam, or overflow explanation.

Acceptance criteria: The improved beat is distinct in playback, dock rules remain unchanged, and EditMode/PlayMode pass.

### 7. Water Forecast/Rise Readability Audit

Goal: Separate "this row is forecast" from "this row just flooded."

What to do: Review water forecast, countdown, rise, water-contact outcome, audio, and timing against recorded play.

Acceptance criteria: A short audit names the first water readability fix and confirms actions, not time, advance danger.

### 8. Implement First Water Readability Improvement

Goal: Make water pressure feel warned and action-based.

What to do: Improve one forecast or rise feedback beat without changing water rules or level timing.

Acceptance criteria: Forecast and rise read as different states, invalid taps still do not advance water, and EditMode/PlayMode pass.

### 9. Level-Authoring Tool Audit

Goal: Reduce fragile manual steps in authoring, verifying, tuning, replaying, and inspecting levels.

What to do: Review current scripts, validators, solve tooling, previews, replay output, and capture notes for friction.

Acceptance criteria: A ranked tool-improvement list exists with one narrow first implementation task.

### 10. First Cold-Player Test

Goal: Check whether Phase 2A changes improve interpretation with real players.

What to do: Run 3-5 cold players through the current packet or focused proof levels and ask the Phase 1 playtest questions.

Acceptance criteria: Notes record whether at least 3 players can explain rescue path goal, action-based danger, dock failure, water/vine warnings, and rescue-puzzle identity.

## Guardrail

Phase 2A is not a content-expansion phase. If a proposed task adds mechanics, meta systems, monetization, new hazards, District 2, or broad refactors, pause and restate how it directly improves readability, animation/feedback, authoring throughput, or capture proof before proceeding.
