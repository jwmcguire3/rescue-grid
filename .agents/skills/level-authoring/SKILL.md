---
name: level-authoring
description: Author, modify, or validate Phase 1 Rescue Grid level JSON files. Use when creating new levels, editing levels in Assets/Resources/Levels/, debugging a level that fails validation, or writing a new tile grid. Covers the level schema, tile-code grammar, per-level design intents for L01-L15, the validator CLI workflow, and common authoring mistakes.
---

# Level Authoring — Rescue Grid Phase 1

## Purpose

This skill is the authoring contract for Phase 1 levels. Every level in `Assets/StreamingAssets/Levels/` must conform to it. The goal of Phase 1 is to prove four things:

1. Acting advances danger; thinking is free.
2. Dock tension feels self-authored.
3. Rescue order is the central puzzle.
4. Rescue extraction feels different from generic level completion.

If a level you author doesn't serve one of those four proofs, it probably shouldn't exist. Levels are diagnostic, not content.

## When to use this skill

- Creating a new level JSON file
- Editing an existing level's tile grid, blocker layout, or water timing
- Debugging a validator error on a level file
- Writing test levels for new rules or regression coverage
- Reviewing a Codex-generated level for design correctness

Do not use this skill for engine changes, validator rule changes, or schema extensions. Those are C1/C2 concerns, not authoring.

## Schema summary

Full schema documentation lives in `Assets/Rescue/Content/README.md` and is authoritative. This is the working summary.

```
{
  "id": "L03",
  "name": "Rescue order arrives",
  "board": {
    "width": 6,
    "height": 7,
    "tiles": [
      [".", ".", "A", "B", ".", "."],
      ...
    ]
  },
  "debrisTypePool": ["A", "B", "C", "D"],
  "baseDistribution": null,
  "targets": [
    { "id": "T0", "row": 5, "col": 1 },
    { "id": "T1", "row": 1, "col": 4 }
  ],
  "initialFloodedRows": 0,
  "water": { "riseInterval": 10 },
  "vine": {
    "growthThreshold": 4,
    "growthPriority": [ { "row": 3, "col": 2 } ]
  },
  "dock": {
    "size": 7,
    "jamEnabled": true
  },
  "assistance": {
    "chance": 0.70,
    "consecutiveEmergencyCap": 2
  },
  "meta": {
    "intent": "Force first clear priority between near-water puppy and easier-but-safer puppy.",
    "expectedPath": "Save lower puppy first even though upper puppy looks more open.",
    "expectedFailMode": "Save the easier puppy first, lose lower puppy to water.",
    "whatItProves": "By Level 3, rescue order is the puzzle."
  }
}
```

### Field rules

- `id`: `L01` through `L15` for Phase 1. Must match filename without extension.
- `debrisTypePool`: 4 entries for L01–L04, 5 entries for L05–L15. Must be distinct. Letters from `A`–`E`.
- `baseDistribution`: `null` for even distribution (default). Only use weighted distributions if the level intent calls for it explicitly.
- `water.riseInterval`: `0` means water is disabled for this level (Phase 1 uses this only on L02).
- `dock.size`: always `7` in Phase 1. Do not change.
- `dock.jamEnabled`: `true` for L01 and L02 only. `false` for L03–L15.
- `assistance.chance`: value from the band table in the tuning section below.
- `vine`: include the block even on levels with no vines (use `growthThreshold: 4, growthPriority: []`). Simpler than making the field optional.
- `meta`: every field required. `notes` is optional. Write these as if someone else has to debug the level without talking to you.

## Tile-code grammar

| Code   | Meaning                        | Notes                                                    |
|--------|--------------------------------|----------------------------------------------------------|
| `.`    | Empty tile                     | Gravity settles into these                               |
| `A`–`E`| Debris of that type            | Must appear in `debrisTypePool`                          |
| `CR`   | Crate                          | Breaks on 1 adjacent clear                               |
| `CX`   | Reinforced crate               | Breaks on 2 adjacent clears. **Off by default in Phase 1.** Only use if approved during late-packet tuning. |
| `I<X>` | Ice with debris X underneath   | E.g. `IA` = ice revealing A on break. X must be in pool. |
| `V`    | Vine                           | Breaks on 1 adjacent clear. Grows per `vine.growthThreshold`. |
| `T<n>` | Target with id `T<n>`          | `n` is a single digit, 0-9. Must match a `targets[]` entry. |

### Grammar rules

- Row 0 is the **top** of the board. Water rises from the bottom (highest row index).
- `tiles` is row-major: `tiles[row][col]`.
- `tiles.length` must equal `board.height`. Every `tiles[row].length` must equal `board.width`.
- Target tile codes must correspond to a `targets[]` entry with matching coord and id.
- Ice hidden codes (`X` in `I<X>`) must be a debris letter present in `debrisTypePool`.
- Phase 1 does not support multi-digit target ids. Max 10 targets per level, which is far more than any Phase 1 level needs.
- Do not use reinforced crates (`CX`) without explicit approval. If you find yourself wanting one, you are probably solving the wrong problem — adjust crate count or placement instead.

## Tuning tables (from Phase 1 package section 2)

Use these unless the level's intent explicitly requires otherwise. Deviation must be justified in `meta.notes`.

### Water timing by level band

| Levels | `initialFloodedRows` | `water.riseInterval` |
|--------|----------------------|----------------------|
| L01    | 0                    | 12                   |
| L02    | 0                    | 0 (disabled)         |
| L03–L04| 0                    | 10                   |
| L05–L06| 0                    | 9                    |
| L07–L09| 1                    | 8                    |
| L10–L12| 1                    | 7                    |
| L13–L15| 1–2                  | 6                    |

**Rule:** raise `initialFloodedRows` before lowering `riseInterval` below 6. Do not go below 6 in Phase 1 under any circumstance. If a level needs more pressure than the table allows, the level is wrong, not the table.

### Assistance chance by level band

| Levels   | `assistance.chance` |
|----------|---------------------|
| L01–L03  | 0.70                |
| L04–L06  | 0.60                |
| L07–L10  | 0.45                |
| L11–L15  | 0.30                |

`assistance.consecutiveEmergencyCap` is always `2` in Phase 1.

### Vine growth threshold

| Vine role        | `growthThreshold` |
|------------------|-------------------|
| Introduction     | 4                 |
| Pressure         | 3                 |

Vine growth fires once per trigger (one new tile per growth event). Never simultaneous spreads.

### Debris pool size

| Levels   | `debrisTypePool` count |
|----------|------------------------|
| L01–L04  | 4                      |
| L05–L15  | 5                      |

## Per-level design intents (Phase 1 package section 3)

These are the authored design intents for all 15 Phase 1 levels. Do not invent new intents when authoring these levels — execute these.

### L01 — First rescue
- **Board:** 6×7. 1 puppy upper-middle. 6 crates. 4 debris types.
- **Water:** starts at 0 rows, 12 actions/row. Dock Jam on.
- **Intent:** teach tap group, dock clear, rescue extraction, water is coming.
- **Expected path:** clear lower-center pairs, open direct lane, free puppy before second water rise.
- **Expected fail mode:** overfill dock while chasing easy side groups.
- **Proves:** the game can deliver "save the puppy" before it reads as abstract clearing.

### L02 — Dock pressure
- **Board:** 6×7 with one narrow middle lane. 1 puppy. 8 crates. 4 debris types, denser singles.
- **Water:** none. Dock Jam on.
- **Intent:** teach that the dock is not a bag of free storage.
- **Expected path:** clear with dock discipline, avoid hoarding mismatched singles.
- **Expected fail mode:** trigger Dock Jam; survive or fail through poor rack sequencing.
- **Proves:** losses can read as self-authored even with no hazard present.

### L03 — Rescue order arrives
- **Board:** 6×7 split lower-left / upper-right. 2 puppies. 6 crates. 4 debris types.
- **Water:** 10 actions/row.
- **Intent:** force first clear priority between near-water puppy and easier-but-safer puppy.
- **Expected path:** save lower puppy first even though upper puppy looks more open.
- **Expected fail mode:** save the easier puppy first, lose lower puppy to water.
- **Proves:** by Level 3, rescue order is the puzzle. **This is the most important level in the packet. Author it with extra care.**

### L04 — Ice introduction
- **Board:** 6×7. 1 puppy. 4 crates. 4 ice. 4 debris types.
- **Water:** 10 actions/row.
- **Intent:** teach revealed future value and adjacency literacy.
- **Expected path:** break ice on urgent lane before cashing easier dock sets elsewhere.
- **Expected fail mode:** ignore frozen lane, run out of time opening route.
- **Proves:** ice reads immediately and does not muddle the seed.

### L05 — Sequencing with mixed blockers
- **Board:** 6×8. 2 puppies. Crates + ice. 5 debris types.
- **Water:** 9 actions/row.
- **Intent:** combine order choice with blocker choice.
- **Expected path:** open lower target through ice first, then pivot top target.
- **Expected fail mode:** spend too many actions on crate-only side because it looks cleaner.
- **Proves:** rescue order survives once the board gets slightly messier.

### L06 — First bigger read
- **Board:** 7×8. 2 puppies. 10 blockers mixed crate/ice. 5 debris types.
- **Water:** 9 actions/row.
- **Intent:** test first-read readability on a larger board.
- **Expected path:** take central lane, not the broad outer clear.
- **Expected fail mode:** broad side clears feel productive but waste action budget.
- **Proves:** the player can orient in a larger greybox without the game turning mushy.

### L07 — Vine introduction, static first
- **Board:** 7×8. 1 puppy. 5 crates. 3 vines. 5 debris types.
- **Water:** 8 actions/row.
- **Vine:** growth off (use `growthThreshold: 999` or similar — the validator accepts large thresholds). Or set the vine count such that no growth priority entries exist.
- **Intent:** teach vine as visible route blocker before it starts pressuring.
- **Expected path:** clear vine lane because it is obviously shortest.
- **Expected fail mode:** treat vine as "just another tile" and slow down route.
- **Proves:** vine can enter as pressure visualization, not confusion.

### L08 — Vine growth tutorial
- **Board:** 7×8. 1 puppy. 4 crates. 4 vines. 5 debris types.
- **Water:** 8 actions/row. Vine growth threshold: 4.
- **Intent:** teach that ignoring vine creates future cost.
- **Expected path:** cut vine when preview appears, then continue route.
- **Expected fail mode:** ignore preview, let vine close the clean lane.
- **Proves:** route urgency can be visual and fair.

### L09 — Order plus vine pressure
- **Board:** 7×8 split into two approach pockets. 2 puppies. Crates + vines. 5 debris types.
- **Water:** 8 actions/row. Vine threshold: 4.
- **Intent:** make player choose between the lower water threat and the lane that vine is about to worsen.
- **Expected path:** solve water-near puppy first, clip one vine on the way.
- **Expected fail mode:** tunnel on vine side and lose the lower puppy.
- **Proves:** the prototype can create triage, not just obstacle management.

### L10 — First packet midpoint exam
- **Board:** 7×8 with central choke. 2 puppies. Mixed crates/ice/vines. 5 debris types.
- **Pressure:** 1 row pre-flooded. Water 7 actions/row.
- **Intent:** pressure first meaningful route planning under all current rules.
- **Expected path:** take choke quickly, then branch.
- **Expected fail mode:** over-collect dock value before opening choke.
- **Proves:** all three blockers can coexist without hiding the seed.

### L11 — False-easy target trap
- **Board:** 7×9. 2 puppies — one visually open high target, one buried low target. Mixed blockers.
- **Pressure:** 1 row pre-flooded. Water 7 actions/row.
- **Intent:** punish "finish what looks easiest" thinking.
- **Expected path:** route to buried lower puppy first.
- **Expected fail mode:** rescue high open puppy, lose buried lower puppy.
- **Proves:** order remains legible even when the board tempts the wrong answer.

### L12 — Three-target readability test
- **Board:** 7×9 broad board. 3 puppies. Moderate blockers. 5 debris types.
- **Pressure:** 1 row pre-flooded. Water 7 actions/row.
- **Intent:** first true triage board.
- **Expected path:** lower-left, then center, then top-right.
- **Expected fail mode:** players try to half-solve all three and save none efficiently.
- **Proves:** players can still verbalize order and attribution under higher cognitive load.

### L13 — Vine pressure exam
- **Board:** 7×9 with one authored growth lane. 2 puppies. Heavier vines, light crates/ice.
- **Pressure:** 1 row pre-flooded. Water 6 actions/row. Vine threshold: 3.
- **Intent:** make the player respect vine as future action tax.
- **Expected path:** clip vine twice early, then finish urgent rescue.
- **Expected fail mode:** let vine grow, then get action-starved and overflow dock while rerouting.
- **Proves:** vine supports the seed rather than becoming a side mechanic.

### L14 — Late packet stress test
- **Board:** 8×9. 3 puppies. Dense mixed blockers. 5 debris types.
- **Pressure:** 1 row pre-flooded. Water 6 actions/row. Vine threshold: 3.
- **Intent:** determine whether the system still feels fair when difficulty rises sharply.
- **Expected path:** commit fully to one rescue, pivot hard to second, ignore tempting low-value clears.
- **Expected fail mode:** brute-force clearing or indecision causes either dock fail or water fail.
- **Proves:** the seed scales into real tension instead of collapsing into busywork.

### L15 — Capture level / ad moment
- **Board:** 8×9 with obvious flooded lower kennel and spotlight puppy. 1 hero puppy + 1 secondary puppy. Clean authored sightline. Light mixed blockers.
- **Pressure:** 2 rows pre-flooded. Water 6 actions/row.
- **Intent:** create the most understandable high-stakes rescue beat for footage and final concept proof.
- **Expected path:** clear visible wrong-side bait once, realize it, then take leash-side urgent route.
- **Expected fail mode:** chase bait side and lose hero moment.
- **Proves:** the game has one genuinely captureable "I know exactly what to do here" rescue sequence from real play. **Authoring note: this level must be readable in 10 seconds by someone who has never played. Prioritize sightline clarity over tricky design.**

## Authoring workflow

### 1. Write the level JSON

Files live in `Assets/StreamingAssets/Levels/` as `L01.json` through `L15.json`. Filename must match the `id` field.

Start from the per-level intent block above. Copy the tuning values from the band tables. Lay out the tile grid last — it's the most iterative part.

### 2. Validate locally

Run the standalone validator CLI (it does not require Unity):

```
./scripts/validate-levels.sh
```

Or for a single file:

```
dotnet run --project Tools/LevelValidator -- validate Assets/StreamingAssets/Levels/L03.json
```

To see the board as ASCII for sanity-checking:

```
dotnet run --project Tools/LevelValidator -- preview Assets/Resources/Levels/L03.json
```

The validator exits non-zero on any `Error`-level issue. Warnings do not block.

### 3. Playtest in the debug panel

Open the Unity project, load the scene with the debug panel (`F1` to toggle). Pick the level from the dropdown. Play through it. Confirm:

- The expected path in `meta` actually works
- The expected fail mode is actually reachable
- The level completes within the water budget when played correctly
- The level fails cleanly when played wrongly

If the expected path doesn't work, the level is wrong. Fix the level, not the intent.

### 4. Commit

One level per commit. Commit message format: `level: L03 — rescue order arrives`. Reference the Phase 1 package section 3 intent if the level deviates from it.

## Common mistakes

These are the authoring errors that show up most often. Check against this list before asking for review.

### Validator errors

- **Tile grid size mismatch.** `tiles.length != board.height`, or a row length mismatch. Count rows and columns manually; JSON arrays are easy to miscount.
- **Debris code not in pool.** A tile uses `E` but `debrisTypePool` only has `A`–`D`. Happens when adapting a 5-pool level down to 4.
- **Ice hidden code not in pool.** `IE` in a 4-pool level. Same root cause as above.
- **Target coord/id mismatch.** `targets[]` says `T0` is at `(5, 1)` but `tiles[5][1]` is `T1`. Keep the two in sync every time you move a target.
- **Vine growth priority out of bounds.** Easy to miss because the validator only checks bounds, not reachability.
- **Out-of-band assistance chance.** `0.7` is correct for L01–L03; `70` is wrong. It's a fraction, not a percent.

### Design errors (validator won't catch these)

- **Level 3 without meaningful order pressure.** If both puppies can be rescued comfortably in either order, the level fails its purpose. The near-water puppy must be genuinely at risk if rescued second.
- **Ice introduction on a level where ice doesn't matter.** L04 must require breaking ice on the urgent lane. If the player can route around ice, it's not teaching ice.
- **Vine growth priority that the player never sees.** The growth target should be visible and on a route the player might actually want to use. Authored growth onto a dead corner teaches nothing.
- **Dock pressure through sheer debris count instead of composition.** Easy to make a level fail by overloading the board. The goal on L02 is tight dock sequencing, not "too many pieces."
- **Water rises that outpace the intended solve.** If the expected path requires 18 actions and water destroys the target at action 20, there's no margin for error. Aim for a 2–4 action margin on correct play.
- **Three-target level with a dominant strategy.** L12 should reward triage thinking. If one rescue order is strictly best, you've authored a sequence puzzle, not a triage puzzle.

### Schema edge cases worth calling out

- **L02 has `water.riseInterval: 0`** to disable water. Don't leave the field out; set it explicitly to `0`.
- **Levels without vines still include the `vine` block** with `growthThreshold: 4` and empty `growthPriority: []`. Simpler than making the field optional.
- **L07 vine growth should be effectively disabled.** Use a very high `growthThreshold` (e.g. `999`) or author `growthPriority: []` so no growth fires. The level exists to teach vine-as-static-blocker before growth is introduced in L08.
- **Dock Jam (`dock.jamEnabled: true`) is for L01 and L02 only.** Every other level sets it to `false`.

## Ask before acting if

- A level's design intent seems to conflict with the tuning table. The intent wins; document the deviation in `meta.notes`.
- You need to introduce reinforced crates (`CX`). This is off by default in Phase 1 and requires explicit approval.
- The validator surfaces a warning you don't understand. Don't suppress it; investigate. Warnings often flag real playability issues.
- A level requires more than 3 puppies or fewer than 1. Phase 1 is scoped for 1–3 puppies per level.
- You're tempted to author a level outside the L01–L15 set. Phase 1 is exactly 15 levels. Extras are Phase 2 concerns.
- You're tempted to add a new tile code, blocker, or hazard. That's a schema change, not an authoring change. Stop and escalate.

## What this skill does NOT cover

- Engine or rule logic changes (see Batch B)
- Validator rule additions (see Task C1)
- Debug panel changes (see Task C2)
- Telemetry schema (see Task C5)
- Visual assets, animations, or art (not in Phase 1 scope beyond one Mae portrait and one extraction animation)

If a request touches any of those, decline and point to the right task.
