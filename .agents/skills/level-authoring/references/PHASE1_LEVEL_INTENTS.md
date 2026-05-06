# L00-L15 Production Onboarding Intents

Use this reference when modifying L00-L15 production onboarding levels. `docs/phase_1_spec.md` remains authoritative for Phase 1 rules and should be checked before changing these intents.

These levels preserve the Phase 1 proof goals while also supporting first-session retention, confidence, density, and tension/release pacing. They are not a separate temporary packet: L00 is the production rule-teach opener, and L01-L15 are production onboarding inside the first campaign.

Current Phase 1 packet membership is governed by `docs/level-packets/phase1.packet.json`, and current per-level intent is governed by `docs/level-briefs/`. Use this file as L00-L15 onboarding intent support, not as the complete L00-L20 packet authority.

## Production onboarding requirements

- Teach, prove, and sell the core game: acting advances danger; thinking is free; dock pressure is self-authored; rescue order is central; extraction is the payoff.
- Preserve the tuning bands below unless a level intent explicitly requires a deviation. Document deviations in `meta.notes`.
- Follow `FIRST_100_PACING_MODEL.md` for tension/release rhythm. Do not run L00-L15 as a flat difficulty ramp.
- Begin visually complete by default. Empty cells must serve authored geometry, environmental negative space, hazard space, spawn corridors, deliberate rescue routes, target readability, or mobile readability.
- Make density/readability expectations explicit in level `meta`. Do not create exact JSON boards from this file alone.

## Tuning bands

Use these unless the level intent explicitly requires a deviation. Document deviations in `meta.notes`.

### Water timing

| Levels | Initial flooded rows | Water rise interval |
|--------|----------------------|---------------------|
| L00 | 0 | positive rule-teach value |
| L01 | 0 | 12 |
| L02 | 0 | 0, disabled |
| L03-L04 | 0 | 10 |
| L05-L06 | 0 | 9 |
| L07-L09 | 1 | 8 |
| L10-L12 | 1 | 7 |
| L13-L15 | 1-2 | 6 |

Raise `initialFloodedRows` before lowering `water.riseInterval` below 6. Do not go below 6 in Phase 1 outside the L00 rule-teach special case.

### Assistance

| Levels | Assistance chance |
|--------|-------------------|
| L01-L03 | 0.70 |
| L04-L06 | 0.60 |
| L07-L10 | 0.45 |
| L11-L15 | 0.30 |

`assistance.consecutiveEmergencyCap` is always 2 in Phase 1 unless the spec changes.

### Debris pool

| Levels | Debris type pool count |
|--------|------------------------|
| L00-L04 | 5 |
| L05-L15 | 6 |

## L00 - Production rule-teach opener

- Role: Teach.
- Primary skill: recognize that accepted actions advance danger while thinking is free.
- Secondary skill: make the first rescue route hypothesis on a real-looking board.
- Board: compact authored opener, visually complete enough to feel like production play.
- Composition: 1 puppy, clear first valid pair, 5 debris types, density target 60-75%.
- Pressure: water is held before the first valid action, then normal action ticking begins.
- Tension beat: visible water forecast makes the first action feel consequential.
- Release beat: the opener immediately shows that the player can control the danger by choosing well.
- Intent: teach the action-driven hazard rule without making the board read as a sparse tutorial diagram.
- Expected path: take the visible opening move and see water advance because of the action.
- Expected fail mode: not a punishment level; failure should not be the lesson.
- Density/readability: use local clustering, target placement, water forecast, and route framing for clarity; avoid large empty regions whose only job is pointing at the lesson.
- Proves: the action-driven hazard rule is legible before L01 while still feeling like the real game.

## L01 - First rescue

- Role: Teach.
- Primary skill: complete the first rescue extraction.
- Secondary skill: learn tap groups, dock clear basics, and incoming water.
- Board: 6x7, 1 puppy upper-middle, 6 crates, 5 debris types.
- Composition: early production density target 70-80%.
- Pressure: 0 flooded rows, water every 12 actions, Dock Jam on.
- Tension beat: water forecast establishes urgency without punishing deliberation.
- Release beat: first extraction should feel immediate, emotional, and confidence-building.
- Intent: teach tap group, dock clear, rescue extraction, and incoming water.
- Expected path: clear lower-center pairs, open direct lane, free puppy before second water rise.
- Expected fail mode: overfill dock while chasing easy side groups.
- Density/readability: preserve a readable direct lane while filling the surrounding board with meaningful dock and route choices.
- Proves: the game can deliver "save the puppy" before it reads as abstract clearing.

## L02 - Dock discipline serves rescue

- Role: Practice.
- Primary skill: manage dock residue.
- Secondary skill: keep dock discipline attached to a rescue purpose.
- Board: 6x7 with one narrow middle lane, 1 puppy, 8 crates, 5 debris types, denser singles.
- Composition: early production density target 70-80%.
- Pressure: water disabled, Dock Jam on.
- Tension beat: the dock becomes dangerous through the player's own sequencing.
- Release beat: disciplined clears open the rescue lane and make the rack lesson feel useful.
- Intent: teach that the dock is not a bag of free storage.
- Expected path: clear with dock discipline and avoid hoarding mismatched singles.
- Expected fail mode: trigger Dock Jam; survive or fail through poor rack sequencing.
- Density/readability: keep the lane legible without stripping away surrounding valid choices; the board should not look like a dock-only lesson.
- Proves: losses can read as self-authored even with no hazard present.

## L03 - Rescue order arrives

- Role: Choice.
- Primary skill: choose the urgent rescue first.
- Secondary skill: compare water threat against an easier-looking route.
- Board: 6x7 split lower-left and upper-right, 2 puppies, 6 crates, 5 debris types.
- Composition: early production density target 70-80%.
- Pressure: water every 10 actions.
- Tension beat: lower puppy is clearly closer to danger than the safer-looking upper puppy.
- Release beat: saving the urgent puppy first creates the first strong "I picked the right rescue" moment.
- Intent: force first clear priority between near-water puppy and easier-but-safer puppy.
- Expected path: save lower puppy first even though upper puppy looks more open.
- Expected fail mode: save the easier puppy first and lose lower puppy to water.
- Density/readability: target priority must be readable at a glance, but both routes need enough surrounding material to feel like real gameplay.
- Proves: by Level 3, rescue order is the puzzle.

This is the most important early onboarding identity level. Author it with extra care.

## L04 - Ice introduction

- Role: Teach.
- Primary skill: read ice as future value on a rescue route.
- Secondary skill: use adjacency to reveal useful debris.
- Board: 6x7, 1 puppy, 4 crates, 4 ice, 5 debris types.
- Composition: early production density target 70-80%.
- Pressure: water every 10 actions.
- Tension beat: the frozen lane must matter before easier dock value elsewhere.
- Release beat: breaking ice reveals the next useful step and makes planning feel clever.
- Intent: teach revealed future value and adjacency literacy.
- Expected path: break ice on the urgent lane before cashing easier dock sets elsewhere.
- Expected fail mode: ignore frozen lane and run out of time opening route.
- Density/readability: ice must be visibly on the rescue route, not buried as decorative clutter; surrounding debris should keep the board complete.
- Proves: ice reads immediately and does not muddle the seed.

## L05 - Ice practice and release

- Role: Release.
- Primary skill: practice ice investment with lower emotional punishment after L03-L04.
- Secondary skill: keep rescue order present through a softer two-target pivot.
- Board: 6x8, 2 puppies, crates plus ice, 6 debris types.
- Composition: early-to-mid density target 75-85%.
- Pressure: water every 9 actions.
- Tension beat: the lower route still asks for early ice work, but the board should feel more generous than a mixed-blocker exam.
- Release beat: breaking ice pays off cleanly and gives a satisfying rescue sequence.
- Intent: let the player practice ice as future value while maintaining rescue-order awareness.
- Expected path: open lower target through ice first, then pivot top target.
- Expected fail mode: spend too many actions on crate-only side because it looks cleaner.
- Density/readability: keep ice payoff obvious and avoid stacking blockers so tightly that this becomes pure sequencing pressure.
- Proves: rescue order survives once the board gets slightly messier, and ice can feel rewarding rather than punishing.

## L06 - First bigger read

- Role: Practice.
- Primary skill: orient on a larger board.
- Secondary skill: choose a central rescue lane over broad low-value clearing.
- Board: 7x8, 2 puppies, 10 blockers mixed crate/ice, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: water every 9 actions.
- Tension beat: the larger shape creates a real first-read choice.
- Release beat: a good central-lane read opens progress quickly.
- Intent: test first-read readability on a larger board.
- Expected path: take central lane, not the broad outer clear.
- Expected fail mode: broad side clears feel productive but waste action budget.
- Density/readability: increased board size must not become visual mush; routes and target states should remain readable on mobile.
- Proves: the player can orient in a larger greybox without the game turning mushy.

## L07 - Vine introduction, static first

- Role: Teach.
- Primary skill: understand vine as a visible route blocker.
- Secondary skill: preserve rescue route priority under one pre-flooded row.
- Board: 7x8, 1 puppy, 5 crates, 3 vines, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 8 actions, vine growth effectively disabled.
- Tension beat: flooded row plus vine lane frames the shortest rescue path.
- Release beat: clearing the vine lane immediately makes the route feel intentional.
- Intent: teach vine as visible route blocker before it starts pressuring.
- Expected path: clear vine lane because it is obviously shortest.
- Expected fail mode: treat vine as just another tile and slow down route.
- Density/readability: vines must be visually isolated enough to read as route blockers while the rest of the board remains playable and complete.
- Proves: vine can enter as pressure visualization, not confusion.

## L08 - Vine growth tutorial

- Role: Teach.
- Primary skill: respond to warned vine growth.
- Secondary skill: continue rescue route planning after clipping vine.
- Board: 7x8, 1 puppy, 4 crates, 4 vines, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 8 actions, vine threshold 4.
- Tension beat: the preview shows a clean lane about to become more expensive.
- Release beat: clipping vine resets the pressure and keeps the rescue route fair.
- Intent: teach that ignoring vine creates future cost.
- Expected path: cut vine when preview appears, then continue route.
- Expected fail mode: ignore preview and let vine close the clean lane.
- Density/readability: vine preview and target route must not compete with ice/crate noise; make the warned growth attributable.
- Proves: route urgency can be visual and fair.

## L09 - Order plus vine pressure

- Role: Pressure Choice.
- Primary skill: compare water-near rescue priority against warned vine cost.
- Secondary skill: clip one vine without losing the main rescue order.
- Board: 7x8 split into two approach pockets, 2 puppies, crates plus vines, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 8 actions, vine threshold 4.
- Tension beat: lower water threat and worsening vine lane pull attention in different directions.
- Release beat: saving the water-near puppy while clipping vine gives a clean triage payoff.
- Intent: make player choose between lower water threat and a lane that vine is about to worsen.
- Expected path: solve water-near puppy first, clip one vine on the way.
- Expected fail mode: tunnel on vine side and lose the lower puppy.
- Density/readability: both pockets must be readable as real options, with the urgent target still clearly more endangered.
- Proves: production onboarding can create triage, not just obstacle management.

## L10 - Mid-onboarding exam

- Role: Exam.
- Primary skill: plan a rescue route under all current rules.
- Secondary skill: keep dock value subordinate to opening the choke.
- Board: 7x8 with central choke, 2 puppies, mixed crates/ice/vines, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 7 actions.
- Tension beat: central choke creates the first real all-systems planning test.
- Release beat: passing the choke should feel like "I understand the core game."
- Intent: pressure first meaningful route planning under all current rules.
- Expected path: take choke quickly, then branch.
- Expected fail mode: over-collect dock value before opening choke.
- Density/readability: all three blockers may coexist, but the board must still read as one rescue problem.
- Proves: all three blockers can coexist without hiding the seed.

## L11 - Recovery choice

- Role: Recovery.
- Primary skill: rebuild confidence after the mid-onboarding exam through a readable target-order choice.
- Secondary skill: notice that the easier-looking rescue is not always first.
- Board: 7x9, 2 puppies, one visually open high target and one buried low target, mixed blockers.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 7 actions.
- Tension beat: high target offers a tempting but lower-priority path.
- Release beat: choosing the buried lower puppy first feels like recovered control, not punishment.
- Intent: reinforce "compare before committing" after L10 without making the level punishment-heavy.
- Expected path: route to buried lower puppy first.
- Expected fail mode: rescue high open puppy and lose buried lower puppy.
- Density/readability: the false-easy target should be tempting but not deceptive; target danger and route costs must remain legible.
- Proves: order remains legible even when the board tempts the wrong answer.

## L12 - Three-target readability test

- Role: Choice.
- Primary skill: verbalize a three-target rescue order.
- Secondary skill: avoid spreading partial progress across every target.
- Board: 7x9 broad board, 3 puppies, moderate blockers, 6 debris types.
- Composition: mid production density target 75-85%.
- Pressure: 1 flooded row, water every 7 actions.
- Tension beat: three targets create cognitive load without introducing new mechanics.
- Release beat: a clear first rescue stabilizes the rest of the plan.
- Intent: first true triage board.
- Expected path: lower-left, then center, then top-right.
- Expected fail mode: players try to half-solve all three and save none efficiently.
- Density/readability: each target route needs distinct visual logic; do not let the broad board become noisy or underfilled.
- Proves: players can still verbalize order and attribution under higher cognitive load.

## L13 - Vine pressure exam

- Role: Exam.
- Primary skill: respect vine as warned future action tax.
- Secondary skill: balance vine clipping with urgent rescue progress.
- Board: 7x9 with one authored growth lane, 2 puppies, heavier vines, light crates/ice.
- Composition: late onboarding density target 80-90% unless readability requires documented restraint.
- Pressure: 1 flooded row, water every 6 actions, vine threshold 3.
- Tension beat: vine preview threatens to tax the route while water keeps the rescue urgent.
- Release beat: early vine clipping creates a readable, satisfying route opening.
- Intent: make the player respect vine as future action tax.
- Expected path: clip vine twice early, then finish urgent rescue.
- Expected fail mode: let vine grow, then get action-starved and overflow dock while rerouting.
- Density/readability: vine pressure must be forecasted and attributable; reduce competing blocker noise before reducing route readability.
- Proves: vine supports the seed rather than becoming a side mechanic.

## L14 - Late onboarding stress test

- Role: Pressure.
- Primary skill: commit to rescue order under sharp but fair late-onboarding tension.
- Secondary skill: ignore low-value clears and pivot decisively.
- Board: 8x9, 3 puppies, dense mixed blockers, 6 debris types.
- Composition: late onboarding density target 80-90%.
- Pressure: 1 flooded row, water every 6 actions, vine threshold 3.
- Tension beat: higher density and three targets create a real stress test.
- Release beat: a successful pivot to the second rescue should be visibly rewarding.
- Intent: determine whether the system still feels fair when difficulty rises sharply.
- Expected path: commit fully to one rescue, pivot hard to second, ignore tempting low-value clears.
- Expected fail mode: brute-force clearing or indecision causes either dock fail or water fail.
- Density/readability: dense does not mean cluttered; target order, water forecast, and at least one strong first route must remain readable.
- Proves: the seed scales into real tension instead of collapsing into busywork.

## L15 - Onboarding spectacle / hero rescue

- Role: Spectacle.
- Primary skill: execute a high-stakes hero rescue with clear sightline.
- Secondary skill: reject an empty-board shortcut and commit to the real urgent route.
- Board: 8x9 with obvious flooded lower kennel and spotlight puppy, 1 hero puppy plus 1 secondary puppy, clean authored sightline, light mixed blockers.
- Composition: late onboarding density target 80-90%, with any readability exception justified in `meta.notes`.
- Pressure: 2 flooded rows, water every 6 actions.
- Tension beat: the hero puppy is visibly endangered and the correct route is emotionally obvious.
- Release beat: extraction should create onboarding payoff: relief, clarity, and a rescue worth remembering.
- Intent: create a readable high-stakes hero rescue that sells the game as rescue-first production onboarding.
- Expected path: avoid empty-board shortcuts; read the sightline, clear the urgent route, and rescue the hero puppy before resolving the secondary puppy.
- Expected fail mode: chase bait side or generic clears and lose the hero moment.
- Density/readability: prioritize 10-second sightline clarity without leaving most of the board empty; the level should be readable in footage and still play honestly.
- Proves: the game has a memorable "I know exactly what to do here" rescue sequence from real play.

This level must be readable in 10 seconds by someone who has never played. Prioritize emotional clarity and sightline clarity over tricky design, but do not solve clarity with an empty board.
