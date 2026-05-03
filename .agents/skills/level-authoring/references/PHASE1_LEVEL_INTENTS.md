# Phase 1 Level Intents

Use this reference when modifying L00-L15 prototype levels. `docs/phase_1_spec.md` remains authoritative for Phase 1 rules and should be checked before changing these intents.

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

## L00 - Rule teach

- Board: small authored opener.
- Composition: 1 puppy, clear first valid pair, 5 debris types.
- Pressure: water is held before the first valid action, then normal action ticking begins.
- Intent: teach that acting advances danger and thinking is free.
- Expected path: take the visible opening move and see water advance because of the action.
- Expected fail mode: not a punishment level; failure should not be the lesson.
- Proves: the action-driven hazard rule is legible before the main packet begins.

## L01 - First rescue

- Board: 6x7, 1 puppy upper-middle, 6 crates, 5 debris types.
- Pressure: 0 flooded rows, water every 12 actions, Dock Jam on.
- Intent: teach tap group, dock clear, rescue extraction, and incoming water.
- Expected path: clear lower-center pairs, open direct lane, free puppy before second water rise.
- Expected fail mode: overfill dock while chasing easy side groups.
- Proves: the game can deliver "save the puppy" before it reads as abstract clearing.

## L02 - Dock discipline serves rescue

- Board: 6x7 with one narrow middle lane, 1 puppy, 8 crates, 5 debris types, denser singles.
- Pressure: water disabled, Dock Jam on.
- Intent: teach that the dock is not a bag of free storage.
- Expected path: clear with dock discipline and avoid hoarding mismatched singles.
- Expected fail mode: trigger Dock Jam; survive or fail through poor rack sequencing.
- Proves: losses can read as self-authored even with no hazard present.

## L03 - Rescue order arrives

- Board: 6x7 split lower-left and upper-right, 2 puppies, 6 crates, 5 debris types.
- Pressure: water every 10 actions.
- Intent: force first clear priority between near-water puppy and easier-but-safer puppy.
- Expected path: save lower puppy first even though upper puppy looks more open.
- Expected fail mode: save the easier puppy first and lose lower puppy to water.
- Proves: by Level 3, rescue order is the puzzle.

This is the most important proof level in the packet. Author it with extra care.

## L04 - Ice introduction

- Board: 6x7, 1 puppy, 4 crates, 4 ice, 5 debris types.
- Pressure: water every 10 actions.
- Intent: teach revealed future value and adjacency literacy.
- Expected path: break ice on the urgent lane before cashing easier dock sets elsewhere.
- Expected fail mode: ignore frozen lane and run out of time opening route.
- Proves: ice reads immediately and does not muddle the seed.

## L05 - Sequencing with mixed blockers

- Board: 6x8, 2 puppies, crates plus ice, 6 debris types.
- Pressure: water every 9 actions.
- Intent: combine order choice with blocker choice.
- Expected path: open lower target through ice first, then pivot top target.
- Expected fail mode: spend too many actions on crate-only side because it looks cleaner.
- Proves: rescue order survives once the board gets slightly messier.

## L06 - First bigger read

- Board: 7x8, 2 puppies, 10 blockers mixed crate/ice, 6 debris types.
- Pressure: water every 9 actions.
- Intent: test first-read readability on a larger board.
- Expected path: take central lane, not the broad outer clear.
- Expected fail mode: broad side clears feel productive but waste action budget.
- Proves: the player can orient in a larger greybox without the game turning mushy.

## L07 - Vine introduction, static first

- Board: 7x8, 1 puppy, 5 crates, 3 vines, 6 debris types.
- Pressure: 1 flooded row, water every 8 actions, vine growth effectively disabled.
- Intent: teach vine as visible route blocker before it starts pressuring.
- Expected path: clear vine lane because it is obviously shortest.
- Expected fail mode: treat vine as just another tile and slow down route.
- Proves: vine can enter as pressure visualization, not confusion.

## L08 - Vine growth tutorial

- Board: 7x8, 1 puppy, 4 crates, 4 vines, 6 debris types.
- Pressure: 1 flooded row, water every 8 actions, vine threshold 4.
- Intent: teach that ignoring vine creates future cost.
- Expected path: cut vine when preview appears, then continue route.
- Expected fail mode: ignore preview and let vine close the clean lane.
- Proves: route urgency can be visual and fair.

## L09 - Order plus vine pressure

- Board: 7x8 split into two approach pockets, 2 puppies, crates plus vines, 6 debris types.
- Pressure: 1 flooded row, water every 8 actions, vine threshold 4.
- Intent: make player choose between lower water threat and a lane that vine is about to worsen.
- Expected path: solve water-near puppy first, clip one vine on the way.
- Expected fail mode: tunnel on vine side and lose the lower puppy.
- Proves: the prototype can create triage, not just obstacle management.

## L10 - First packet midpoint exam

- Board: 7x8 with central choke, 2 puppies, mixed crates/ice/vines, 6 debris types.
- Pressure: 1 flooded row, water every 7 actions.
- Intent: pressure first meaningful route planning under all current rules.
- Expected path: take choke quickly, then branch.
- Expected fail mode: over-collect dock value before opening choke.
- Proves: all three blockers can coexist without hiding the seed.

## L11 - False-easy target trap

- Board: 7x9, 2 puppies, one visually open high target and one buried low target, mixed blockers.
- Pressure: 1 flooded row, water every 7 actions.
- Intent: punish "finish what looks easiest" thinking.
- Expected path: route to buried lower puppy first.
- Expected fail mode: rescue high open puppy and lose buried lower puppy.
- Proves: order remains legible even when the board tempts the wrong answer.

## L12 - Three-target readability test

- Board: 7x9 broad board, 3 puppies, moderate blockers, 6 debris types.
- Pressure: 1 flooded row, water every 7 actions.
- Intent: first true triage board.
- Expected path: lower-left, then center, then top-right.
- Expected fail mode: players try to half-solve all three and save none efficiently.
- Proves: players can still verbalize order and attribution under higher cognitive load.

## L13 - Vine pressure exam

- Board: 7x9 with one authored growth lane, 2 puppies, heavier vines, light crates/ice.
- Pressure: 1 flooded row, water every 6 actions, vine threshold 3.
- Intent: make the player respect vine as future action tax.
- Expected path: clip vine twice early, then finish urgent rescue.
- Expected fail mode: let vine grow, then get action-starved and overflow dock while rerouting.
- Proves: vine supports the seed rather than becoming a side mechanic.

## L14 - Late packet stress test

- Board: 8x9, 3 puppies, dense mixed blockers, 6 debris types.
- Pressure: 1 flooded row, water every 6 actions, vine threshold 3.
- Intent: determine whether the system still feels fair when difficulty rises sharply.
- Expected path: commit fully to one rescue, pivot hard to second, ignore tempting low-value clears.
- Expected fail mode: brute-force clearing or indecision causes either dock fail or water fail.
- Proves: the seed scales into real tension instead of collapsing into busywork.

## L15 - Capture level / concept proof

- Board: 8x9 with obvious flooded lower kennel and spotlight puppy, 1 hero puppy plus 1 secondary puppy, clean authored sightline, light mixed blockers.
- Pressure: 2 flooded rows, water every 6 actions.
- Intent: create the most understandable high-stakes rescue beat for footage and final concept proof.
- Expected path: clear visible wrong-side bait once, realize it, then take the urgent route.
- Expected fail mode: chase bait side and lose hero moment.
- Proves: the game has one captureable "I know exactly what to do here" rescue sequence from real play.

This level must be readable in 10 seconds by someone who has never played. Prioritize sightline clarity over tricky design.
