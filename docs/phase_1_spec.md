# Phase 1 Package — Rescue Grid Implemented Rules Authority

## 0. Locked design calls for Phase 1

These are the Phase 1 rules and scope calls. This document is the implemented gameplay rules/design authority for the Phase 1 prototype.

Exact current packet membership and packet policy are governed by `docs/level-packets/phase1.packet.json`. Current per-level intent is governed by `docs/level-briefs/`. If packet membership or briefs differ from the historical level-count language in this document, use the manifest and briefs for content scope while preserving the rules here.

Phase 1 exists to prove whether Rescue Grid is a rescue-order puzzle with dock pressure and per-action hazards, not a smaller tray sorter with puppy dressing.

### Keep

- One primary hazard: water
- Three blockers: crate, ice, vine
- One target family: puppies
- One free undo per level
- One minimal Mae cameo/portrait reaction set
- One real extraction animation
- Player-visible target state progression
- 15 main packet levels plus L00 rule-teach opener
- Persistent water forecast
- Fixed 7-slot dock
- Dock Jam as a teaching variant on Levels 1–2 only
- Spreading vine as a Phase 1 pressure system, with strict forecast/readability requirements
- Assisted spawns, with telemetry tagging and replay visibility

### Cut

- Fire, overgrowth, freeze fog
- Tools, keys, relics, tool-gated rescues
- Lucky drops
- Power-ups beyond free undo
- Distinct dock sizes
- Full meta loop, full sanctuary, economy, live ops, pass, cosmetics
- Continuation offers
- Monetization pressure
- Any mechanic whose value is “more content” rather than “better proof”

### Specific calls

The dock stays 7 slots for the full packet. Do not vary size in pass one. Size variation is a second-pass tuning knob, not a first-pass truth test.

Dock Jam stays only as a teaching variant on Levels 1–2. After that, overflow is real.

Phase 1 must support two water-contact test modes:

1. **Immediate-loss mode:** water reaching an unrescued target fails the level.
2. **One-tick grace mode:** water reaching an unrescued target puts that target into Distressed for one recoverable action.

Both modes must run from the same authored levels and same telemetry schema. The goal is to learn whether immediate water failure is clean and fair, or whether the grace beat is required for calm urgency.

Final-target rescue beats dock overflow and water advance if the final target becomes extractable from the accepted action. Saved-on-the-last-action is sacred.

Target extraction happens before gravity and spawn. An open target state cannot be refilled away before extraction. Once a target becomes extractable, that eligibility latches until extraction resolves.

Reinforced crate exists only as a data flag (`crate_hp = 2`) and is off by default. Do not use it unless late packet tuning proves the three core blockers are insufficient.

### v3.2 alignment note for Phase 1

Adopted in this prototype spec:

- Level 0 rule-teach
- Persistent next-flood-row forecast for water
- Player-visible target states
- One-clear-away target state as a real rules state and visible rescue-readiness state
- Extraction before hazard advance
- Win immediately on final rescue
- Assisted spawn tagging for trust analysis

Not adopted in Phase 1:

- Insertion preview
- Full distressed-state production layer beyond the one-tick grace test mode
- Continuation offers
- Lucky drops
- RNG-bias acknowledgment copy
- Full sanctuary restoration loop
- Broader production-law, monetization, and live-ops systems

---

## 1. Phase 1 prototype rules spec

## 1.1 Core purpose of the ruleset

This ruleset exists to prove four things:

1. Acting advances danger; thinking is free.
2. Dock tension feels self-authored.
3. Rescue order is the central puzzle, not decoration.
4. Rescue extraction feels different from generic level completion.

The prototype fails if players describe the game primarily as rack management, cleanup, or sorting.

Desired player language:

- “I picked the wrong rescue first.”
- “I overfilled the dock.”
- “I had time to think.”
- “I saved the puppy because I planned the route.”

Not desired:

- “The timer got me.”
- “I got unlucky.”
- “It’s just tray sort with puppies.”
- “I opened the puppy and the game still said no.”

---

## 1.2 Board contents

### Movable pieces

- Debris only in Phase 1
- 5 debris types in Levels 0–4
- 6 debris types in Levels 5–15

### Targets

- 1–3 puppies per level
- Targets occupy a tile
- Targets are not movable
- Targets are not directly tappable
- Targets extract automatically when their required orthogonal neighbors are open
- Extractable state latches immediately and cannot be undone by gravity/spawn

### Blockers

- **Crate:** breaks after 1 adjacent clear
- **Ice:** breaks after 1 adjacent clear; reveals the frozen piece underneath
- **Vine:** clears after 1 adjacent clear; spreads on its own timer if not interrupted

### Not present

- No tools
- No resource pieces
- No keys
- No switches
- No board transformers
- No power-up economy
- No paid rescue continuation
- No cosmetic or sanctuary economy

---

## 1.3 Start state density

Rescue Grid levels begin visually complete by default. The opening board should look like real gameplay, not a tutorial diagram.

Empty cells are allowed only when they are part of authored board geometry, environmental negative space, hazard space, spawn corridors, readability around a target, or a deliberately shaped rescue route.

Empty cells must never appear as arbitrary missing pieces whose only purpose is to point at a lesson.

Teaching should be done through board shape, target placement, blocker framing, piece distribution, first-move legibility, dock setup, water forecast, target state progression, and hazard timing.

The first playable impression should be: "I am already playing the real game."

### Density targets

| Level type | Target visual occupancy |
|---|---:|
| L00 rule-teach exception | 60-75% |
| Early Phase 1 levels | 70-80% |
| Mid Phase 1 levels | 75-85% |
| Late Phase 1 levels | 80-90% |

Visual occupancy includes debris, blockers, targets, vines, ice, flooded rows, and any supported authored non-playable/environment cells.

A level below target must justify the exception in `meta.notes`.

Density is a design-review rule, not only a validator rule.

---

## 1.4 Group definition and valid input

A valid group is:

- 2 or more orthogonally adjacent debris tiles
- Same type
- Exposed and tappable

A tap on a single tile is invalid:

- no state change
- no dock change
- no blocker damage
- no hazard advance
- no vine counter advance
- small reject bump/audio only

Invalid taps must not feel like hidden punishment.

---

## 1.5 Action pipeline

This order is a locked rules contract. If action order is fuzzy, fairness becomes fuzzy.

### Per-action resolution order

1. **Input accepted**
   - Player taps a valid group.
   - Save full pre-action snapshot for undo.

2. **Group removal**
   - Tapped debris leaves the board immediately.
   - Removed group is recorded for dock insertion later in this action.

3. **Blocker damage check**
   - Any blocker with at least one orthogonally adjacent cleared tile takes 1 hit max per action.
   - Multiple adjacencies in one action do not stack.

4. **Blocker break resolution**
   - Broken crate disappears.
   - Broken ice disappears and reveals underlying piece.
   - Cleared vine disappears.
   - If any vine is cleared, the vine counter resets for this action.

5. **Target state update and extraction latch**
   - All targets update their visible rescue-readiness state.
   - Any open required orthogonal neighbor of an unextracted target becomes locked rescue-path empty.
   - Locked rescue-path empty tiles remain reserved through gravity and spawn until that target extracts.
   - Debris may fall through locked rescue-path empty tiles, but cannot land on them or replace them.
   - Any target with all required orthogonal neighbors open becomes extractable immediately.
   - Extractable state latches now.
   - Latching happens before dock insertion, gravity, spawn, and hazard advance.
   - A latched target cannot become un-extractable because pieces later fall or spawn around it.

6. **Dock insertion**
   - Removed group is precalculated before dock insertion.
   - Complete triples within the removed group are consumed immediately and do not enter the dock.
   - Only the removed group remainder enters dock left-to-right (`group size % 3`).
   - The dock receives this precalculated remainder even if the action also opened a target.

7. **Dock clear check**
   - Dock scans by type.
   - Every complete set of 3 clears.
   - If 4 of a type are present, 3 clear and 1 remains.
   - If 6 are present, two triples clear.
   - Remaining pieces compact left.
   - Dock overflow status is determined after this clear/compaction step.

8. **Target extraction resolution**
   - Any latched extractable target extracts.
   - Target is removed from the board.
   - Extraction animation plays.
   - Mae portrait reacts.
   - Extraction order is logged.
   - If this action extracts the final remaining target, the level is won immediately.

9. **Final rescue win check**
   - If all targets have extracted, level completes immediately.
   - No gravity.
   - No spawn.
   - No hazard tick.
   - No water rise.
   - No dock overflow failure from this same action overrides the final rescue.
   - Exception: a level already frozen in an unresolved pre-action failure state cannot be rescued by acting unless the rules explicitly allow an undo/recovery state.

10. **Dock overflow loss check**
   - If the dock exceeds 7 slots after dock clear/compaction and the level is not already won, resolve dock overflow.
   - Dock Jam applies only on Levels 1–2 and only if unused.
   - If Dock Jam does not apply, the level fails here.
   - Dock overflow is checked before gravity, spawn, and hazard resolution so the player understands that rack failure was the cause.

11. **Gravity settle**
   - Dry, active pieces fall into empty dry spaces.
   - Locked rescue-path empty tiles are transparent to falling debris, but remain reserved and unoccupied.
   - Gravity may include controlled diagonal settling if enabled by the Gravity and Deadboard Integrity Addendum.
   - Gravity does not affect already-latched extraction.

12. **Spawn**
   - New pieces spawn from top into dry space only.
   - Spawn may pass through locked rescue-path empty tiles to fill reachable empty dry spaces, but cannot occupy the locked tiles.
   - If a column enters through a locked rescue-path tile above a target, spawn also passes over that target to replenish empty dry spaces below it; the target itself is never occupied.
   - Spawn assistance may apply according to tuning rules.
   - Assisted spawns must be tagged in telemetry.

13. **Hazard counters tick**
   - Water counter +1.
   - Vine counter +1 if no vine was cleared this action.
   - If any vine was cleared, vine counter resets and does not advance this action.

14. **Hazard resolution**
   - Water may rise.
   - Vine may grow.
   - Hazard preview/warning events update.

15. **Water target consequence**
   - If water reaches an unrescued target:
     - Immediate-loss mode: level fails.
     - One-tick grace mode: target enters Distressed if not already Distressed; if already Distressed and still not rescued, level fails.
   - Final-target extraction already won before this step and cannot be overturned.

16. **Return control**
   - If no win/loss state is active, control returns to the player.

### Visual priority

The visual priority of a rescue action is:

1. The board opens and the puppy visibly reacts.
2. Dock cost resolves clearly.
3. The puppy extracts.
4. The level wins if that was the final target.
5. Only if the level is not won do gravity, spawn, and hazards continue.

Do not show hazard advance before dock resolution. Do not show gravity/spawn before target extraction. Do not let presentation imply that a puppy was freed and then arbitrarily re-trapped.

---

## 1.6 Dock logic

### Dock size

Fixed at 7.

### Insertion

- Complete triples within the removed group are consumed before dock insertion.
- Groups occupy slots equal to their remainder after complete removed-group triples are consumed.
- A group of 3 or 6 occupies 0 dock slots; a group of 4 occupies 1 slot; a group of 5 occupies 2 slots.
- Pieces enter in group order.
- Dock compacts only after clears.

### Clear rule

- Any full set of 3 identical pieces clears after each action.
- Clearing can resolve multiple triples in one step.

### Warnings

| Occupancy | Meaning | Presentation |
|---|---|---|
| 0–4 | Safe | Neutral |
| 5/7 | Caution | Amber edge glow |
| 6/7 | Acute | Red pulse + heavier haptic + slight dock shake |
| >7 after clear/compaction | Fail if not final-rescue win | Freeze + shake + loss explanation |

### Dock Jam

Levels 1–2 only.

First overflow in the level freezes the board. Player gets exactly 1 more action to clear at least one triple. If they do, play continues. If not, lose normally.

Dock Jam only occurs once per level.

Reason: teach the consequence without muting it for the rest of the packet.

### Final rescue exception

If the accepted action latches and extracts the final target, that final rescue wins even if the same action would otherwise overflow the dock. This prevents the game from punishing the player after the emotional rescue climax.

This exception does not apply to non-final targets. If the player rescues one puppy but overflows the dock while other puppies remain, the level can still fail from dock overflow.

---

## 1.7 Hazard behavior — water

Water is the only primary hazard in Phase 1.

### Level 0 rule-teach special case

L00 is the first interaction in the prototype packet. It exists only to teach “acting advances danger; thinking is free.”

In L00, water is held before the first valid action. On the first valid action, the hold is released and the normal per-action water rule resumes immediately.

The authored teaching beat is a visible opening pair that causes the first water rise.

### Water model

- Water rises from the bottom of the board upward.
- It advances by rows, not tiles.
- It advances by action threshold, not time.
- When water rises, the next dry row becomes flooded across its full width.

### Flooded row behavior

- Flooded tiles become inactive.
- Debris or blockers in flooded rows are no longer interactable.
- Gravity resolves only in remaining dry space.
- Spawn enters dry space only.
- A target in a row that floods triggers the configured water-contact consequence unless the target has already latched/extracted.

### Water-contact test modes

Phase 1 must support both modes.

#### Mode A: Immediate loss

If water reaches an unrescued target, the level fails during hazard consequence resolution.

Purpose: tests the cleanest and harshest attribution model.

#### Mode B: One-tick grace

If water reaches an unrescued target for the first time, that target enters Distressed. Distressed gives the player one recoverable action.

If a Distressed target is still unrescued when water consequence resolves again, the level fails.

Purpose: tests whether recoverable hazard contact better supports calm urgency and reduces “one move away” resentment.

### Final rescue rule

If the action latches the final target before water advances, the level wins. Water does not advance after final rescue. Final rescue beats water rise.

### Why row-based flooding

It is the cleanest readable version of “danger is coming,” especially in greybox. It creates rescue-order pressure without requiring more simulation.

### Water visibility

The prototype exposes a persistent forecast of the next flood row.

At minimum, the player should be able to read which row floods next without waiting for a threshold-1 warning.

This is a readability/forecast affordance, not a promise of final-game waterline art.

Threshold-1 warning layers on top:

- next row flashes blue
- subtle water audio swell
- debug/event surfacing may also call out the pending rise

This is how the game avoids reading like a hidden timer.

### Water escalation

No within-level acceleration in pass one.

Escalation happens across the packet, not inside individual levels.

Reason: within-level acceleration risks “pseudo-timer” feel before the seed is proven.

---

## 1.8 Blocker behavior

### Crate

- 1 hit from adjacent clear
- disappears
- simplest route tax

### Ice

- 1 hit from adjacent clear
- reveals underlying debris
- teaches adjacency literacy and future-value reading

### Vine

- 1 hit from adjacent clear
- removed on break
- spreading vine is included in Phase 1, but it is treated as a hazard-class pressure system for readability and telemetry

### Vine growth rules

- Global vine counter
- If the player clears any vine, the counter resets and does not advance that action
- Clearing any vine resets/cancels the current vine growth plan
- If the player does not clear vine, the counter advances during the hazard counter step
- On threshold, exactly 1 vine tile grows
- Vine growth is deterministic and uses a planned growth goal, never random spread
- Vine growth plans toward rescue-relevant pressure
- Candidate pressure includes actual rescue path tiles and possible rescue-route neighbor cells around unextracted/unlatched targets
- Growth proceeds one valid tile at a time from an existing vine/frontier toward the planned goal
- Authored `vine.growthPriority` remains allowed as bias, constraint, tutorial lane, or fallback
- One action before growth, the intended growth tile pulses/animates

This is important: vine must be readable pressure, not noise. Growth must feel warned, deterministic, and attributable.

This is a refinement of the existing Phase 1 vine behavior, not a new hazard, blocker, or mechanic.

### Vine growth threshold

| Level use | Growth threshold | Preview |
|---|---:|---|
| Intro vine | 4 actions | 1 action early |
| Pressure vine | 3 actions | 1 action early |

### Growth cap

One growth tile total per growth event. Never multiple simultaneous spreads in Phase 1.

---

## 1.9 Rescue logic

### Extraction trigger

A target extracts when all required orthogonal neighbors are open.

- Interior target: 4 sides
- Edge target: all existing orthogonal sides
- Corner target: 2 sides

Open required neighbors become locked rescue-path empty as soon as target state updates. These tiles are visually distinct from ordinary empty spaces and cannot be occupied by falling debris or spawn before the rescue resolves. Falling debris may pass through them to lower empty spaces.

### Extraction latch

Extraction eligibility is evaluated immediately after group removal and blocker break resolution.

If all required neighbors are open, the target enters extractable state and latches.

Latched extraction cannot be undone by:

- dock insertion
- dock clear
- gravity
- spawn
- water rise
- vine growth

The extraction itself resolves after dock insertion and dock clear, so the player still pays the dock cost for the rescue action.

### Extraction behavior

- Auto-extracts during the target extraction resolution step.
- Removes target from board.
- Plays extraction animation.
- Mae portrait reacts.
- If this was the final target, the level ends immediately.
- One short aftercare card or kennel shot plays after level win.

### Player-visible target states

Phase 1 requires player-visible target state progression. Debug-only surfacing is not sufficient.

#### 1. Trapped

Default state.

Meaning: target is not close to rescue.

Minimum visual:

- curled or worried posture
- dimmer outline or colder lighting
- small idle concern animation
- target portrait neutral/concerned

Purpose: establishes concern.

#### 2. Progressing / Half-cleared

Condition: at least half of required orthogonal neighbors are open.

Minimum visual:

- target lifts head or shifts posture
- subtle brightness/warmth increase
- portrait softens
- optional small relief sound

Purpose: tells the player the route is working before the final move.

#### 3. One-clear-away

Condition: exactly one required orthogonal neighbor remains blocked.

Minimum visual:

- target sits up or makes eye contact
- target/portrait pulse
- one-clear-away event fires
- the remaining blocking neighbor is visually readable as the last obstacle

Purpose: makes the next rescue move emotionally and mechanically legible.

#### 4. Extracting / Rescued

Condition: all required orthogonal neighbors are open and extraction is latched.

Minimum visual:

- target leaves tile
- extraction animation plays
- Mae reacts
- rescued target count updates
- if final target, win begins immediately

Purpose: climax and win trigger.

#### Optional test state: Distressed

Condition: only in one-tick grace mode, when water reaches an unrescued target for the first time.

Minimum visual:

- urgent blue/water danger outline
- scared posture
- Mae concern expression
- clear one-action recovery urgency

Purpose: test whether a recoverable hazard-contact beat improves fairness and emotional tension.

---

## 1.10 Undo rules

One free undo per level.

Undo restores the exact pre-action snapshot:

- board
- dock
- water counter and flood height
- vine counter and vine positions
- extracted targets
- latched extraction states
- Distressed states, if grace mode is active
- warning states
- assisted-spawn state needed for deterministic replay

Can be used after a loss freeze if unused.

Cannot be chained beyond one action back.

No purchase. No recharge.

This is mandatory protection against mis-tap frustration and aligns with the scope lock.

---

## 1.11 Win and loss conditions

### Win condition

A level wins when all targets have extracted.

If the final target becomes extractable from the accepted action:

- extraction latches immediately
- extraction resolves during the target extraction step
- level completes immediately
- no gravity
- no spawn
- no hazard tick
- no water rise
- no same-action dock overflow failure overrides the win

The level ends on rescue, not on board cleanup.

### Loss conditions

A level fails if any of these are true and no final-rescue win has already triggered:

1. Dock overflow
   - Dock occupancy exceeds 7 after dock clear/compaction.
   - Exception: Dock Jam on Levels 1–2 only.
   - Final-rescue win beats same-action dock overflow.

2. Water reaches an unrescued target
   - Immediate-loss mode: fail on first contact.
   - One-tick grace mode: first contact causes Distressed, second unresolved contact fails.

### Not included

- No move-count fail
- No real-time fail
- No designed deadboard fail

If deadboard occurs, classify it during review:

- true generator bug
- player-authored deadboard
- soft deadboard where legal moves exist but no meaningful rescue route remains
- rescue-impossible state before formal loss

Do not hide deadboard-like states under “generation bug” without diagnosis.

---

# 2. Tuning sheet

## 2.1 Global tuning targets

These are the first-pass knobs.

### Target feel

- Pressure should feel present by Level 3.
- Dock should feel dangerous by Level 2.
- Rescue order should feel central by Level 3.
- First packet should still be readable on a cold mobile session.
- Players should see target state progression before the extraction climax.
- Final rescue should feel like the natural endpoint of the level.

### Desired player language

- “I picked the wrong rescue first.”
- “I overfilled the dock.”
- “I had time to think.”
- “I saved the puppy on the last move.”
- “I knew which puppy was almost free.”

### Not desired

- “The timer got me.”
- “I got unlucky.”
- “It’s just tray sort with puppies.”
- “The puppy was free, but the game didn’t count it.”
- “Water moved after I already saved it.”

---

## 2.2 Water rates

| Level band | Start flooded rows | Rise interval |
|---|---:|---:|
| 1 | 0 | 12 actions |
| 2 | 0 | none |
| 3–4 | 0 | 10 actions |
| 5–6 | 0 | 9 actions |
| 7–9 | 1 | 8 actions |
| 10–12 | 1 | 7 actions |
| 13–15 | 1–2 | 6 actions |

### Rules

Use higher start water before making interval too fast.

Do not go below 6 actions/row in pass one.

If players call it “timed,” first slow the interval before changing anything else.

If players ignore water until cleanup, increase urgency via start flood or target placement before speeding the interval.

---

## 2.3 Spawn weights and assistance

### Base piece pool

- Levels 0–4: 5 debris types (`A`, `B`, `C`, `D`, `F`)
- Levels 5–15: 6 debris types (`A`–`F`)

### Base spawn distribution

Even by default unless the level brief specifies weighted composition.

### Assistance set

The spawn helper can prefer:

- a type with 2 already in dock
- a type that completes a reachable pair near urgent route
- a type adjacent to the most urgent target path

### Assistance strength by band

| Level band | Assistance chance |
|---|---:|
| 1–3 | 70% |
| 4–6 | 60% |
| 7–10 | 45% |
| 11–15 | 30% |

### Emergency assistance

Applies only if:

- dock at 5/7 or worse, or
- urgent target is 1 water rise from loss

Then:

- emergency helper chance becomes +20 points
- max 2 consecutive emergency spawns
- never force an obvious miracle drop directly onto the only winning lane if it looks fake

### Hard rule

If the board risks becoming singletons-only, the generator must surface a legal pair within the next 2 spawns.

### Spawn group integrity

Normal spawning should not create immediate tappable groups of exactly 3. Exact triples are reserved for authored teaching, early-game coaching, dead-board prevention, or explicit level-scripted relief.

Normal spawning should not create fresh groups larger than 5. Groups of 4 and 5 are allowed because they create dock-risk decisions rather than free clears.

Gravity-created groups caused by the player's prior action are allowed, including groups of 3+, because those feel earned and preserve agency.

Mixed spawned/existing groups may exceed the normal cap only when the majority of the group existed before the spawn or when the level explicitly allows it.

### Telemetry requirement

Every assisted spawn must be tagged with:

- assistance type
- trigger condition
- target/dock/hazard context
- whether it affected a subsequent valid move within 2 actions

This is bias in service of readability, not in service of cheap saves. If a proof level only works because assistance repeatedly rescues the route, the level is not proving the design.

---

## 2.4 Dock thresholds

| State | Meaning | Presentation |
|---|---|---|
| 0–4 | safe | neutral |
| 5 | caution | amber edge |
| 6 | acute | red pulse + haptic |
| >7 after dock clear | fail if no final-rescue win | freeze + shake + specific loss explanation |

### Interpretation rule

If players do not mention the dock before the level ends, the dock UI is under-signaling.

If players mention the dock more than the rescue route after Level 3, the dock is overshadowing the game.

---

## 2.5 Vine thresholds

| Level use | Growth threshold | Preview |
|---|---:|---|
| Intro vine | 4 actions | 1 action early |
| Pressure vine | 3 actions | 1 action early |

### Growth cap

One growth tile total per growth event.

Never multiple simultaneous spreads in Phase 1.

### Level-authoring rule

Designers may author vine growth bias/constraints where needed for readability, tutorial lanes, or fallback behavior. They do not need to manually author every vine growth tile. Active vine levels should still prove warned, attributable route pressure and must not rely on hidden or unfair vine targeting.

### Interpretation rule

If players describe vine as random or arbitrary, vine is failing.

If players describe vine more than rescue order in Levels 9–13, vine is stealing the test.

---

## 2.6 Recommended first-pass success rates

| Level band | Target first-attempt win rate |
|---|---:|
| 1–3 | 80–90% |
| 4–6 | 70–80% |
| 7–10 | 60–70% |
| 11–15 | 45–60% |

---

## 2.7 Density and readability tuning

If a level is too easy, do not first make it harder by adding clutter or removing readability.

Preferred difficulty order:

1. sharpen rescue-order pressure,
2. tighten dock residue choices,
3. adjust blocker placement,
4. adjust target placement relative to water,
5. reduce assistance,
6. increase starting flood,
7. reduce water interval within allowed band.

If a level is too sparse, add meaningful board material that supports route choice, dock decisions, rescue readability, or emotional staging. Do not add filler pieces that create noise without decisions.

---

## 2.8 Phase 1 level rebuild note

Existing L00–L15 JSON layouts are draft/proof layouts until they pass the start-state density and readability rules.

Authored playable level JSON lives in `Assets/StreamingAssets/Levels/`. Authored solve/replay JSON lives in `Assets/Resources/Levels/`. Repository onboarding, scene names, and command details belong in `README.md` and `AGENTS.md`; this document remains the gameplay/design authority.

When rebuilding them, preserve each level's purpose, expected path, expected fail mode, water/dock/vine tuning, and proof target unless explicitly changed.

Do not preserve sparse tutorial-diagram geometry merely because it exists in the current JSON.

If a level needs to remain unusually sparse for rule-teach or capture clarity, document the reason in `meta.notes`.

---

# 3. Original L00-L15 level intent packet

This section captures the original L00-L15 packet intent used to prove the Phase 1 rules. It remains useful design history and onboarding intent, but exact current packet membership is governed by `docs/level-packets/phase1.packet.json` and current per-level intent by `docs/level-briefs/`.

I am recommending 15 main packet levels plus L00 as a rule-teach opener. That is enough to scaffold the seed, pressure it, and produce diagnostic data without drifting into content production.

## Level 0 — Rule teach

**Geometry:** 6x7 rectangle  
**Composition:** 1 puppy, light crate framing, 5 debris types, one clearly framed opening pair, and enough surrounding board material to read as a real Rescue Grid board rather than an isolated rule diagram.  
**Pressure:** water held before first valid action, then rises immediately on that action and resumes normal ticking  
**Intent:** teach the core rule through contrast — thinking is free, acting advances danger  
**Expected path:** tap the clearly framed opening pair, observe that acting advances water, then use the next readable route action to free the puppy.  
**Expected fail mode:** player treats the board as static cleanup and misses the authored contrast  
**Density/readability:** L00 may be sparser than normal, but it must still look like a real Rescue Grid board, not a pair of isolated teaching moves.  
**What it proves:** the player can learn the prototype’s defining timing rule through interaction instead of explanation

## Level 1 — First rescue

**Geometry:** 6x7 rectangle  
**Composition:** 1 puppy upper-middle, 6 crates, 5 debris types  
**Pressure:** water starts at 0 rows, 12 actions/row, Dock Jam on  
**Intent:** teach tap group, dock clear, target state progression, rescue extraction, water is coming  
**Expected path:** clear lower-center pairs, see puppy progress state, open direct lane, free puppy before second water rise  
**Expected fail mode:** overfill dock while chasing easy side groups  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** the game can deliver “save the puppy” before it reads as abstract clearing

## Level 2 — Dock discipline serves rescue

**Geometry:** 6x7 with one narrow middle lane  
**Composition:** 1 puppy, 8 crates, 5 debris types, denser singles  
**Pressure:** no water, Dock Jam on  
**Intent:** teach that the dock is not a bag of free storage while keeping the puppy as the reason for the route  
**Expected path:** clear with dock discipline to open the puppy lane, avoid hoarding mismatched singles  
**Expected fail mode:** trigger Dock Jam, survive or fail through poor rack sequencing  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** dock losses can read as self-authored without teaching that the dock is the whole game

## Level 3 — Rescue order arrives

**Geometry:** 6x7 split lower-left / upper-right  
**Composition:** 2 puppies, 6 crates, 5 debris types  
**Pressure:** water 10 actions/row  
**Intent:** force first clear priority between near-water puppy and easier-but-safer puppy  
**Expected path:** save lower puppy first even though upper puppy looks more open  
**Expected fail mode:** save the easier puppy first, lose lower puppy to water  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** by Level 3, rescue order is the puzzle

## Level 4 — Ice introduction

**Geometry:** 6x7  
**Composition:** 1 puppy, 4 crates, 4 ice, 5 debris types  
**Pressure:** water 10 actions/row  
**Intent:** teach revealed future value and adjacency literacy  
**Expected path:** break ice on urgent lane before cashing easier dock sets elsewhere  
**Expected fail mode:** ignore frozen lane, run out of time opening route  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** ice reads immediately and does not muddle the seed

## Level 5 — Sequencing with mixed blockers

**Geometry:** 6x8  
**Composition:** 2 puppies, crates + ice, 6 debris types  
**Pressure:** water 9 actions/row  
**Intent:** combine order choice with blocker choice  
**Expected path:** open lower target through ice first, then pivot top target  
**Expected fail mode:** spend too many actions on crate-only side because it looks cleaner  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** rescue order survives once the board gets slightly messier

## Level 6 — First bigger read

**Geometry:** 7x8  
**Composition:** 2 puppies, 10 blockers mixed crate/ice, 6 debris types  
**Pressure:** water 9 actions/row  
**Intent:** test first-read readability on a larger board  
**Expected path:** take central lane, not the broad outer clear  
**Expected fail mode:** broad side clears feel productive but waste action budget  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** the player can orient in a larger greybox without the game turning mushy

## Level 7 — Vine introduction, static first

**Geometry:** 7x8  
**Composition:** 1 puppy, 5 crates, 3 vines, 6 debris types  
**Pressure:** water 8 actions/row, vine growth off  
**Intent:** teach vine as visible route blocker before it starts pressuring  
**Expected path:** clear vine lane because it is obviously shortest  
**Expected fail mode:** treat vine as just another tile and slow down route  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** vine can enter as pressure visualization, not confusion

## Level 8 — Vine growth tutorial

**Geometry:** 7x8  
**Composition:** 1 puppy, 4 crates, 4 vines, 6 debris types  
**Pressure:** water 8 actions/row, vine grows every 4 untouched actions  
**Intent:** teach that ignoring vine creates future cost  
**Expected path:** cut vine when preview appears, then continue route  
**Expected fail mode:** ignore preview, let vine close the clean lane  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** route urgency can be visual and fair

## Level 9 — Order plus vine pressure

**Geometry:** 7x8 split into two approach pockets  
**Composition:** 2 puppies, crates + vines, 6 debris types  
**Pressure:** water 8 actions/row, vine every 4 untouched actions  
**Intent:** make player choose between the lower water threat and the lane that vine is about to worsen  
**Expected path:** solve water-near puppy first, clip one vine on the way  
**Expected fail mode:** tunnel on vine side and lose the lower puppy  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** the prototype can create triage, not just obstacle management

## Level 10 — First packet midpoint exam

**Geometry:** 7x8 with central choke  
**Composition:** 2 puppies, mixed crates/ice/vines, 6 debris types  
**Pressure:** 1 row pre-flooded, water 7 actions/row  
**Intent:** pressure first meaningful route planning under all current rules  
**Expected path:** take choke quickly, then branch  
**Expected fail mode:** over-collect dock value before opening choke  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** all three blockers can coexist without hiding the seed

## Level 11 — False-easy target trap

**Geometry:** 7x9  
**Composition:** 2 puppies, one visually open high target, one buried low target, mixed blockers  
**Pressure:** 1 row pre-flooded, water 7 actions/row  
**Intent:** punish “finish what looks easiest” thinking  
**Expected path:** route to buried lower puppy first  
**Expected fail mode:** rescue high open puppy, lose buried lower puppy  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** order remains legible even when the board tempts the wrong answer

## Level 12 — Three-target readability test

**Geometry:** 7x9 broad board  
**Composition:** 3 puppies, moderate blockers, 6 debris types  
**Pressure:** 1 row pre-flooded, water 7 actions/row  
**Intent:** first true triage board  
**Expected path:** lower-left, then center, then top-right  
**Expected fail mode:** players try to half-solve all three and save none efficiently  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** players can still verbalize order and attribution under higher cognitive load

## Level 13 — Vine pressure exam

**Geometry:** 7x9 with one authored vine pressure lane  
**Composition:** 2 puppies, heavier vines, light crates/ice  
**Pressure:** 1 row pre-flooded, water 6 actions/row, vine every 3 untouched actions  
**Intent:** make the player respect vine as future action tax  
**Expected path:** clip vine twice early, then finish urgent rescue  
**Expected fail mode:** let vine grow, then get action-starved and overflow dock while rerouting  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** vine supports the seed rather than becoming a side mechanic

## Level 14 — Late packet stress test

**Geometry:** 8x9  
**Composition:** 3 puppies, dense mixed blockers, 6 debris types  
**Pressure:** 1 row pre-flooded, water 6 actions/row  
**Intent:** determine whether the system still feels fair when difficulty rises sharply  
**Expected path:** commit fully to one rescue, pivot hard to second, ignore tempting low-value clears  
**Expected fail mode:** brute-force clearing or indecision causes either dock fail or water fail  
**Density/readability:** Begin visually complete. Empty space must serve route, hazard, geometry, spawn corridor, target readability, or mobile-scale readability. Do not use emptiness merely to point at the lesson.  
**What it proves:** the seed scales into real tension instead of collapsing into busywork

## Level 15 — Capture level / concept proof

**Geometry:** 8x9 with obvious flooded lower kennel and spotlight puppy  
**Composition:** 1 hero puppy + 1 secondary puppy, clean authored sightline, light mixed blockers  
**Pressure:** 2 rows pre-flooded, water 6 actions/row  
**Intent:** create the most understandable high-stakes rescue beat for footage and final concept proof  
**Expected path:** clear visible wrong-side bait once, realize it, then take urgent route  
**Expected fail mode:** chase bait side and lose hero moment  
**Density/readability:** Prioritize immediate sightline clarity for the hero rescue beat, but do not create clarity by leaving most of the board empty.  
**What it proves:** the game has one captureable “I know exactly what to do here” rescue sequence from real play

---

# 4. Playtest hypothesis sheet

## 4.1 Core hypotheses

### Hazard fairness

Players will describe water pressure as fair because it advances only when they act.

Success threshold: at least half of testers explicitly note that they had time to think.

### Rule-teach landing

Players will understand after L00 that water advances on action, not on deliberation.

Success threshold: most first-session testers can restate the rule in their own words after the opening beat.

### Dock attribution

Players will describe dock losses as their mistake, not as randomness.

Success threshold: fewer than 20% of overflow losses are described as “unlucky.”

### Rescue-order readability

By Level 3 of the main packet, most testers will identify rescue order as the core decision.

Success threshold: at least 60% say some version of “I saved the wrong one first.”

### Rescue framing

Players will describe the game as “save the animal before danger gets there,” not just sorting.

Success threshold: 60%+ describe the game as rescue-first.

### Target-state readability

Players will notice target progress before extraction.

Success threshold: at least 60% can identify when a puppy became almost free or one move away.

### Emotional landing

Even with greybox art, extraction plus Mae plus aftercare should create a distinct payoff.

Success threshold: testers mention the puppy, rescue, relief, Mae, or aftercare beat without prompting.

### Not a pseudo-timer

Players should feel pressure without describing the game as timed.

Success threshold: “timed” language appears in under 25% of first-session interviews.

### Final rescue trust

Players should never report that the game invalidated a puppy that looked rescued.

Success threshold: 0 reports of “I opened it and it did not count” caused by event order.

---

## 4.2 Level-specific hypotheses

- L00: Players understand that water moves when they do, not while they think.
- L1: Players call the target a puppy or “the dog,” not “the objective.”
- L1: Players notice at least one target state change before extraction.
- L2: Players understand the dock is dangerous before the level ends.
- L2: Players still understand the puppy is the purpose of the dock discipline.
- L3: Players describe failure as wrong rescue order.
- L3: Players can distinguish the rule-teach from the rescue-order proof; Level 3 should feel like the first pure rescue-priority exam.
- L4: Players understand ice after one exposure without needing text.
- L7: Players see vine as a route issue, not random clutter.
- L8: Most players notice vine preview before the first growth.
- L10: Mixed blocker boards still read as one problem, not three systems.
- L12: Three-target levels create triage, not overload.
- L15: Cold viewers of recorded footage can explain the rescue stakes inside 10 seconds.

---

## 4.3 Questions to ask after play

Ask these verbatim.

- Why did you lose that level?
- What was the most important decision on Level 3?
- Did the water feel fair or annoying?
- Did you ever feel rushed while thinking?
- What was the dock asking you to pay attention to?
- Did saving the puppy feel different from just finishing a level?
- Could you tell when a puppy was almost free?
- Did any puppy ever look free before the game counted it?
- What, if anything, felt random?
- When vine grew, did it feel warned or arbitrary?
- What do you think this game is, in one sentence?
- Would you show this to someone as a rescue game or as a sorting game?

---

## 4.4 Telemetry to log for every level

Minimum useful instrumentation:

- level start
- level win / loss
- loss cause: dock / water / distressed-expired
- action count
- water rises count
- water mode: immediate-loss / one-tick grace
- next flood row / forecast state per action
- dock occupancy per action
- dock overflow would-have-failed flag on final rescue actions
- undo used y/n
- target state transitions:
  - trapped → progressing
  - progressing → one-clear-away
  - one-clear-away → extractable
  - extractable → extracted
  - unrescued → distressed, if grace mode active
- target extraction order
- first target extracted at action #
- target entered one-clear-away at action #
- target extraction latched at action #
- final rescue action #
- vine growth count
- time spent idle between actions
- invalid taps count
- assisted spawn count
- assisted spawn reason
- assisted spawn follow-up usage within 2 actions
- deadboard-like state detected y/n

Without these, post-test interpretation will be too hand-wavy.

---

# 5. Post-test issue taxonomy

This is the triage framework for first external playtests.

## 5.1 Unfair

### What players say

- “That was bullshit.”
- “I had no chance.”
- “It got me even though I knew what to do.”
- “I saved it and then the game killed it.”

### Likely causes

- water interval too fast
- warning too weak or too late
- extraction latch not resolving before hazard
- final rescue not beating water/dock
- emergency spawn help too low
- vine growth preview unreadable

### Primary fix order

1. confirm pipeline ordering
2. confirm extraction latch before gravity/spawn/hazard
3. improve warning clarity
4. slow water by 1 action band
5. compare immediate-loss vs grace mode
6. increase spawn help before changing level layout

---

## 5.2 Unreadable

### What players say

- “I didn’t know what mattered.”
- “There was too much going on.”
- “I couldn’t tell which dog to save first.”
- “I couldn’t tell when the dog was almost free.”

### Likely causes

- target state changes too weak
- target distance differences too subtle
- urgent lane not visually privileged enough
- blocker mix introduced too quickly
- too many debris types too early
- dock warning state under-signaled

### Primary fix order

1. strengthen target state readability
2. simplify first read
3. strengthen path emphasis
4. reduce piece variety
5. reduce simultaneous blocker density

---

## 5.3 Emotionally flat

### What players say

- “Cute, but I was mostly just matching.”
- “Winning felt like finishing a puzzle, not a rescue.”
- “The puppy was just the objective.”

### Likely causes

- target too visually passive
- target state changes missing or too subtle
- extraction animation too short or generic
- Mae absent at wrong moment
- no pre-rescue concern or relief beat
- aftercare shot too weak

### Primary fix order

1. strengthen one-clear-away and extraction
2. strengthen target progress state
3. strengthen Mae cameo timing
4. strengthen aftercare
5. do not add systems; add affective clarity

---

## 5.4 Too generic

### What players say

- “This is tray sort with dogs.”
- “The theme could be anything.”

### Likely causes

- rescue order does not bite by Level 3
- too many boards are really about dock only
- water feels background instead of deciding target priority
- rescue extraction not sufficiently distinct
- target states not visible enough

### Primary fix order

1. sharpen Level 3 and Level 5 order tests
2. reduce pure dock challenge boards after Level 2
3. increase target-priority asymmetry
4. upgrade rescue presentation

---

## 5.5 Too punishing

### What players say

- “I understood it, but it was exhausting.”
- “I got punished for every small mistake.”
- “I was one move away too often.”

### Likely causes

- immediate-loss mode too harsh
- assistance taper too aggressive
- water too high too early
- vine threshold too short
- too many multi-target levels in a row
- same-action final rescue not protected

### Primary fix order

1. compare grace mode to immediate-loss mode
2. increase assistance
3. lower start flood
4. relax vine threshold
5. spread heavy boards apart

---

## 5.6 Too easy to brute-force

### What players say

- “I just kept clearing stuff and won.”
- “I didn’t really need a plan.”

### Likely causes

- water too slow
- targets too close to extraction by default
- dock clears too easy due to color distribution
- wrong-order consequences too soft
- assistance too generous

### Primary fix order

1. raise urgency via start flood before speeding water
2. separate target priorities more clearly
3. reduce obvious dock-fed colors
4. add one more required route pivot
5. reduce assistance on proof levels after readability is stable

---

## 5.7 Vine stealing the test

### What players say

- “I was mostly watching the vines.”
- “The vine thing was the main problem.”
- “The water/dog thing was less important than the spreading stuff.”

### Likely causes

- vine threshold too aggressive
- vine preview too loud
- vine geometry blocks too many paths
- water-rescue order not sharp enough

### Primary fix order

1. reduce vine density
2. relax vine threshold
3. make water-target priority sharper
4. keep spreading vine but delay its exam level

---

## 5.8 Assisted-spawn distrust

### What players say

- “The game gave me exactly what I needed.”
- “I got lucky.”
- “I lost because it stopped giving me matches.”

### Likely causes

- assistance too visible
- emergency spawns too corrective
- proof levels depend on biased spawns
- generator creates soft-deadboards

### Primary fix order

1. inspect assisted-spawn tags
2. replay level with assistance disabled
3. revise geometry/composition before increasing hidden help
4. reserve assistance for readability and anti-deadboard, not route solving

---

## 5.9 Tutorial diagram board

### What players say

- “It felt like a tutorial board.”
- “The answer was being pointed at.”
- “The board looked empty.”
- “It did not feel like the real game yet.”

### Likely causes

- too many empty cells
- teaching through absence instead of composition
- route isolated from the rest of the board
- target surrounded by artificial whitespace
- first move made obvious by deleting alternatives

### Primary fix order

1. Add meaningful surrounding board material.
2. Preserve first-move legibility through local clustering and route framing.
3. Use blockers and target placement to guide attention.
4. Keep empty cells only where they serve route, hazard, geometry, spawn corridor, or readability.
5. Document deliberate sparse exceptions in `meta.notes`.

---

## 5.10 Special red-flag reads

These are kill/pause indicators, not just balance notes.

### Red flag

“It’s basically a sorter with animals.”

Interpretation: seed not landing. Stop adding features.

### Red flag

“I was fighting the rack, not trying to rescue.”

Interpretation: dock overshadowed rescue-order puzzle.

### Red flag

“I knew the answer, but the game didn’t let me do it.”

Interpretation: fairness failure. Probably action order, spawn support, warning clarity, or target latch.

### Red flag

“I opened the dog, then it filled back in.”

Interpretation: extraction timing failure. Target latch or gravity/spawn order is wrong.

### Red flag

“Water moved after I saved it.”

Interpretation: final rescue rule is broken or presentation is lying.

---

# 6. Final recommendation

For Phase 1, do not chase breadth. Chase clarity.

The strongest version of this prototype is:

- 15 levels
- one water hazard
- crate / ice / vine
- 7-slot dock
- one puppy target family
- one Mae reaction set
- one strong extraction beat
- visible target state progression
- extraction before gravity/spawn
- final rescue beats same-action dock overflow and water advance
- immediate-loss and one-tick grace modes both tested
- one brutally clear Level 3 rescue-order proof
- one captureable Level 15 concept-proof moment

If this version does not read as Rescue Grid, adding more systems will not save it. It will only hide the result.

Phase 2 animation and presentation work should proceed only on load-bearing beats:

- valid tap / invalid tap
- group removal
- blocker break
- target state change
- dock insertion and clear
- dock warning and overflow
- water forecast and water rise
- vine preview and vine growth
- target extraction
- Mae reaction
- win/loss explanation
- aftercare stub


## Gravity and Deadboard Integrity Addendum

### Controlled diagonal settling

After vertical gravity resolves, the board may perform controlled diagonal settling to prevent blocked columns from creating unreachable dry voids.

A movable debris piece may slide diagonally down-left or down-right into an empty dry cell only if:

- the destination is dry and active;
- the destination is not flooded;
- the destination is not a blocker;
- the destination is not a target;
- the destination is not rescue-reserved;
- the piece is movable debris;
- vertical fall into that destination is unavailable because the column above is blocked or exhausted.

Diagonal settling occurs during the Gravity settle step, after target extraction and final rescue win check, and before Spawn.

Diagonal settling must be deterministic, replayable, and telemetry-visible.

Diagonal settling must not undo or obscure rescue progress. It may not occupy locked rescue-path empty tiles.

### Runtime no-legal-move protection

At Return Control, the game checks whether at least one valid tappable debris group exists.

If no valid group exists and the level is not already won or lost, the game performs a minimal debris reshuffle.

The reshuffle may only reassign movable debris types among dry, active, non-reserved debris cells.

The reshuffle may not move or alter:

- targets;
- blockers;
- vines;
- flooded rows;
- rescue-reserved tiles;
- dock contents;
- water counters;
- vine counters;
- target states;
- extraction latches;
- undo snapshot data.

The goal is only to restore at least one legal move, not to solve the rescue route.

### Soft-deadboard detection

The game should detect and log soft-deadboard-like states where legal moves exist but no meaningful rescue route appears available.

Soft-deadboards are not automatically fixed in Phase 1 unless they are caused by generator or gravity bugs.

Every deadboard-like event must be tagged as:

- no-legal-move;
- soft-deadboard;
- rescue-impossible;
- player-authored;
- generator-caused;
- gravity-caused;
- spawn-caused;
- level-geometry-caused.

Deadboard repair must be telemetry-visible and replayable.
Do not polish generic board spectacle before the player can explain, in one sentence, why the puppy was saved or lost.
