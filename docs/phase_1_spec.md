Phase 1 package — senior game design
0. Locked design calls for Phase 1

These are the calls I am making so the prototype tests the right thing.

Keep

One hazard: water
Three blockers: crate, ice, vine
One target archetype: puppy
One free undo
One minimal Mae cameo/portrait state
One real extraction animation
12–18 levels; I am recommending 15

Cut

Fire, overgrowth, freeze fog
Tools, keys, relics, tool-gated rescues
Lucky drops
Power-ups beyond free undo
Distinct dock sizes
Meta loop, sanctuary, economy, live ops, pass, cosmetics
Any mechanic whose value is “more content” rather than “better proof”

Specific calls

Dock stays 7 slots for the full packet. Do not vary size in pass one. Size variation is a second-pass tuning knob, not a first-pass truth test.
Dock Jam stays, but only as a teaching variant on Levels 1–2. After that, overflow is real.
No mechanical Distressed state in first-pass build. Water reaching an unrescued target is immediate loss. This is harsher than early full-game softening, but much cleaner for determining whether rescue order is actually the puzzle. If it tests too punitive, the fallback is a one-tick grace state as a tuning toggle, not as the default.
Reinforced crate exists only as a data flag (crate_hp = 2) and is off by default. Do not use it unless late packet tuning proves the three core blockers are insufficient. This keeps the blocker trio conceptually clean.
1. Phase-1 prototype rules spec
1.1 Core purpose of the ruleset

This ruleset exists to prove four things:

Acting advances danger; thinking is free.
Dock tension feels self-authored.
Rescue order is the central puzzle, not decoration.
Rescue extraction feels different from generic level completion.
1.2 Board contents

Movable pieces

Debris only in Phase 1
4 debris types in Levels 1–4
5 debris types in Levels 5–15

Targets

1–3 puppies per level
Targets occupy a tile
Targets are not movable and not tappable

Blockers

Crate: breaks after 1 adjacent clear
Ice: breaks after 1 adjacent clear; reveals the frozen piece underneath
Vine: clears after 1 adjacent clear; spreads on its own timer if not interrupted

Not present

No tools
No resource pieces
No keys
No switches
No board transformers
No power-up economy
1.3 Group definition and valid input

A valid group is:

2 or more orthogonally adjacent debris tiles
Same type
Exposed and tappable

A tap on a single tile is invalid:

no state change
no hazard advance
small reject bump/audio only

Reason: invalid taps should not feel like hidden punishment.

1.4 Action pipeline

This needs to be rigid. If action order is fuzzy, fairness becomes fuzzy.

Per action resolution order
Input accepted
Player taps a valid group
Save full pre-action snapshot for undo
Group removal
Tapped debris leaves the board immediately
Blocker damage check
Any blocker with at least one orthogonally adjacent cleared tile takes 1 hit max per action
Multiple adjacencies in one action do not stack
Blocker break resolution
Broken crate disappears
Broken ice disappears and reveals underlying piece
Cleared vine disappears
Dock insertion
Removed group enters dock left-to-right
Group size equals slot usage
Dock clear check
Dock scans by type
Every complete set of 3 clears
If 4 of a type are present, 3 clear and 1 remains
If 6 are present, two triples clear
Remaining pieces compact left
Gravity settle
Dry, active pieces fall into empty dry spaces
Spawn
New pieces spawn from top into dry space only
Target extraction check
Any target with all required orthogonal neighbors cleared extracts now
Extraction happens before hazard advance
This is crucial for “saved on the last move” fairness
Win check
If all targets extracted, level completes immediately
No post-win hazard step
Hazard counters tick
Water counter +1
Vine counter +1 if no vine was cleared this action
Hazard resolution
Water may rise
Vine may grow
Loss check
Dock overflow
Water hits unrescued target
Return control

This order is the one I would lock with engineering.

1.5 Dock logic

Dock size

Fixed at 7

Insertion

Groups occupy slots equal to group size
Pieces enter in group order; dock compacts only after clears

Clear rule

Any full set of 3 identical pieces clears after each action
Clearing can resolve multiple triples in one step

Warnings

5/7: amber edge glow
6/7: red pulse + heavier haptic + slight dock shake
7/7: freeze and fail state

Dock Jam

Levels 1–2 only
First overflow in the level freezes the board
Player gets exactly 1 more action to clear at least one triple
If they do, play continues
If not, lose normally
Dock Jam only occurs once per level

Reason: teach the consequence without muting it for the rest of the packet.

1.6 Hazard behavior — water

Water is the only hazard in Phase 1.

Water model
Water rises from the bottom of the board upward
It advances by rows, not tiles
It advances by action threshold, not time
When water rises, the next dry row becomes flooded across its full width
Flooded row behavior
Flooded tiles become inactive
Debris or blockers in flooded rows are no longer interactable
Gravity only resolves in remaining dry space
A target in a row that floods is lost immediately
Why row-based flooding

It is the cleanest readable version of “danger is coming,” especially in greybox. It creates strong rescue-order pressure without requiring more simulation.

Water visibility
Persistent waterline at board bottom
Counter pips or fill meter for next rise
On threshold-1:
next row flashes blue
affected target portraits pulse
subtle water audio swell

This is how the game avoids reading like a hidden timer.

Water escalation
No within-level acceleration in pass one
Escalation happens across the packet, not inside individual levels

Reason: within-level acceleration risks “pseudo-timer” feel before the seed is proven.

1.7 Blocker behavior
Crate
1 hit from adjacent clear
disappears
simplest route tax
Ice
1 hit from adjacent clear
reveals underlying debris
teaches adjacency literacy and “future value” reading
Vine
1 hit from adjacent clear
removed on break
Vine growth rules
Global vine counter
If the player clears any vine, the counter resets
If the player does not clear vine, counter advances
On threshold:
exactly 1 vine tile grows
growth uses pre-authored priority list, not random adjacency
One action before growth:
intended growth tile pulses/animates

This is important: vine must be readable pressure, not noise. So growth is authored and telegraphed.

Vine growth target
Growth threshold intro levels: 4 actions
Growth threshold pressure levels: 3 actions
1.8 Rescue logic
Extraction trigger

A target extracts when all required orthogonal neighbors are open.

Interior target: 4 sides
Edge target: all existing orthogonal sides
Corner target: 2 sides
Extraction behavior
Auto-extracts during step 9 of the pipeline
Removes target from board
Plays extraction animation
Mae portrait reacts
One short aftercare card or kennel shot plays after level win
Stabilization beat in prototype

Because Phase 1 has no drain tools or distressed recovery, the true “stabilization” beat from the full game is reduced to a proxy:

When a target becomes one clear away from extraction, play:
target relief animation
portrait pulse
softer vocal
tiny pause

That gives the prototype:

concern
traction
pre-rescue relief
rescue
aftercare

without adding another full rules layer.

1.9 Undo rules
1 free undo per level
Restores exact pre-action snapshot:
board
dock
water counter and flood height
vine counter and vine positions
extracted targets
warning states
Can be used after a loss freeze if unused
Cannot be chained beyond one action back
No purchase, no recharge

This is mandatory protection against mis-tap frustration and aligns with the scope lock.

1.10 Loss conditions

A level fails if either of these is true:

Dock overflow
Dock occupancy exceeds 7 after dock clear/compaction
Exception: Dock Jam Levels 1–2 only
Water reaches any unrescued target
Immediate fail on hazard step

Not included:

No move-count fail
No timer fail
No deadboard fail as a designed state
If deadboard occurs, treat it as a generation bug
2. Tuning sheet
2.1 Global tuning targets

These are the first-pass knobs.

Target feel
Pressure should feel present by Level 3
Dock should feel dangerous by Level 2
Rescue order should feel central by Level 3
First packet should still be readable on a cold mobile session
Desired player language
“I picked the wrong rescue first.”
“I overfilled the dock.”
“I had time to think.”
Not:
“The timer got me.”
“I got unlucky.”
“It’s just tray sort with puppies.”
2.2 Water rates
Level band	Start flooded rows	Rise interval
1	0	12 actions
2	0	none
3–4	0	10 actions
5–6	0	9 actions
7–9	1	8 actions
10–12	1	7 actions
13–15	1–2	6 actions

Rules

Use higher start water before making interval too fast
Do not go below 6 actions/row in pass one
If players call it “timed,” first slow the interval before changing anything else
2.3 Spawn weights and assistance
Base piece pool
Levels 1–4: 4 debris types
Levels 5–15: 5 debris types
Base spawn distribution
Even by default unless the level brief specifies weighted composition
Assistance set

The spawn helper can prefer:

a type with 2 already in dock
a type that completes a reachable pair near urgent route
a type adjacent to the most urgent target path
Assistance strength by band
Level band	Assistance chance
1–3	70%
4–6	60%
7–10	45%
11–15	30%
Emergency assistance

Applies only if:

dock at 5/7 or worse, or
urgent target is 1 water rise from loss

Then:

emergency helper chance becomes +20 points
max 2 consecutive emergency spawns
never force an obvious miracle drop directly onto the only winning lane if it looks fake
Hard rule

If the board risks becoming singletons-only, the generator must surface a legal pair within the next 2 spawns.

This is bias in service of readability, not in service of cheap saves.

2.4 Dock thresholds
State	Meaning	Presentation
0–4	safe	neutral
5	caution	amber edge
6	acute	red pulse + haptic
7	fail	freeze + shake

Interpretation rule
If players do not mention the dock before the level ends, the dock UI is under-signaling.

2.5 Vine thresholds
Level use	Growth threshold	Preview
Intro vine	4 actions	1 action early
Pressure vine	3 actions	1 action early

Growth cap

One growth tile total per growth event
Never multiple simultaneous spreads in Phase 1
2.6 Recommended first-pass success rates
Level band	Target first-attempt win rate
1–3	80–90%
4–6	70–80%
7–10	60–70%
11–15	45–60%
3. Level-by-level intent packet (15-level brief)

I am recommending 15 levels. That is enough to scaffold the seed, pressure it, and still produce diagnostic data without drifting into content production.

Level 1 — First rescue
Geometry: 6x7 rectangle
Composition: 1 puppy upper-middle, 6 crates, 4 debris types
Pressure: water starts at 0 rows, 12 actions/row, Dock Jam on
Intent: teach tap group, dock clear, rescue extraction, water is coming
Expected path: clear lower-center pairs, open direct lane, free puppy before second water rise
Expected fail mode: overfill dock while chasing easy side groups
What it proves: the game can deliver “save the puppy” before it reads as abstract clearing
Level 2 — Dock pressure
Geometry: 6x7 with one narrow middle lane
Composition: 1 puppy, 8 crates, 4 debris types, denser singles
Pressure: no water, Dock Jam on
Intent: teach that the dock is not a bag of free storage
Expected path: clear with dock discipline, avoid hoarding mismatched singles
Expected fail mode: trigger Dock Jam, survive or fail through poor rack sequencing
What it proves: losses can read as self-authored even with no hazard present
Level 3 — Rescue order arrives
Geometry: 6x7 split lower-left / upper-right
Composition: 2 puppies, 6 crates, 4 debris types
Pressure: water 10 actions/row
Intent: force first clear priority between near-water puppy and easier-but-safer puppy
Expected path: save lower puppy first even though upper puppy looks more open
Expected fail mode: save the easier puppy first, lose lower puppy to water
What it proves: by Level 3, rescue order is the puzzle
Level 4 — Ice introduction
Geometry: 6x7
Composition: 1 puppy, 4 crates, 4 ice, 4 debris types
Pressure: water 10 actions/row
Intent: teach revealed future value and adjacency literacy
Expected path: break ice on urgent lane before cashing easier dock sets elsewhere
Expected fail mode: ignore frozen lane, run out of time opening route
What it proves: ice reads immediately and does not muddle the seed
Level 5 — Sequencing with mixed blockers
Geometry: 6x8
Composition: 2 puppies, crates + ice, 5 debris types
Pressure: water 9 actions/row
Intent: combine order choice with blocker choice
Expected path: open lower target through ice first, then pivot top target
Expected fail mode: spend too many actions on crate-only side because it looks cleaner
What it proves: rescue order survives once the board gets slightly messier
Level 6 — First bigger read
Geometry: 7x8
Composition: 2 puppies, 10 blockers mixed crate/ice, 5 debris types
Pressure: water 9 actions/row
Intent: test first-read readability on a larger board
Expected path: take central lane, not the broad outer clear
Expected fail mode: broad side clears feel productive but waste action budget
What it proves: the player can orient in a larger greybox without the game turning mushy
Level 7 — Vine introduction, static first
Geometry: 7x8
Composition: 1 puppy, 5 crates, 3 vines, 5 debris types
Pressure: water 8 actions/row, vine growth off
Intent: teach vine as visible route blocker before it starts pressuring
Expected path: clear vine lane because it is obviously shortest
Expected fail mode: treat vine as “just another tile” and slow down route
What it proves: vine can enter as pressure visualization, not confusion
Level 8 — Vine growth tutorial
Geometry: 7x8
Composition: 1 puppy, 4 crates, 4 vines, 5 debris types
Pressure: water 8 actions/row, vine grows every 4 untouched actions
Intent: teach that ignoring vine creates future cost
Expected path: cut vine when preview appears, then continue route
Expected fail mode: ignore preview, let vine close the clean lane
What it proves: route urgency can be visual and fair
Level 9 — Order plus vine pressure
Geometry: 7x8 split into two approach pockets
Composition: 2 puppies, crates + vines, 5 debris types
Pressure: water 8 actions/row, vine every 4 untouched actions
Intent: make player choose between the lower water threat and the lane that vine is about to worsen
Expected path: solve water-near puppy first, clip one vine on the way
Expected fail mode: tunnel on vine side and lose the lower puppy
What it proves: the prototype can create triage, not just obstacle management
Level 10 — First packet midpoint exam
Geometry: 7x8 with central choke
Composition: 2 puppies, mixed crates/ice/vines, 5 debris types
Pressure: 1 row pre-flooded, water 7 actions/row
Intent: pressure first meaningful route planning under all current rules
Expected path: take choke quickly, then branch
Expected fail mode: over-collect dock value before opening choke
What it proves: all three blockers can coexist without hiding the seed
Level 11 — False-easy target trap
Geometry: 7x9
Composition: 2 puppies, one visually open high target, one buried low target, mixed blockers
Pressure: 1 row pre-flooded, water 7 actions/row
Intent: punish “finish what looks easiest” thinking
Expected path: route to buried lower puppy first
Expected fail mode: rescue high open puppy, lose buried lower puppy
What it proves: order remains legible even when the board tempts the wrong answer
Level 12 — Three-target readability test
Geometry: 7x9 broad board
Composition: 3 puppies, moderate blockers, 5 debris types
Pressure: 1 row pre-flooded, water 7 actions/row
Intent: first true triage board
Expected path: lower-left, then center, then top-right
Expected fail mode: players try to half-solve all three and save none efficiently
What it proves: players can still verbalize order and attribution under higher cognitive load
Level 13 — Vine pressure exam
Geometry: 7x9 with one authored growth lane
Composition: 2 puppies, heavier vines, light crates/ice
Pressure: 1 row pre-flooded, water 6 actions/row, vine every 3 untouched actions
Intent: make the player respect vine as future action tax
Expected path: clip vine twice early, then finish urgent rescue
Expected fail mode: let vine grow, then get action-starved and overflow dock while rerouting
What it proves: vine supports the seed rather than becoming a side mechanic
Level 14 — Late packet stress test
Geometry: 8x9
Composition: 3 puppies, dense mixed blockers, 5 debris types
Pressure: 1 row pre-flooded, water 6 actions/row
Intent: determine whether the system still feels fair when difficulty rises sharply
Expected path: commit fully to one rescue, pivot hard to second, ignore tempting low-value clears
Expected fail mode: brute-force clearing or indecision causes either dock fail or water fail
What it proves: the seed scales into real tension instead of collapsing into busywork
Level 15 — Capture level / ad moment
Geometry: 8x9 with obvious flooded lower kennel and spotlight puppy
Composition: 1 hero puppy + 1 secondary puppy, clean authored sightline, light mixed blockers
Pressure: 2 rows pre-flooded, water 6 actions/row
Intent: create the most understandable high-stakes rescue beat for footage and final concept proof
Expected path: clear visible wrong-side bait once, realize it, then take leash-side urgent route
Expected fail mode: chase bait side and lose hero moment
What it proves: the game has one genuinely captureable “I know exactly what to do here” rescue sequence from real play
4. Playtest hypothesis sheet
4.1 Core hypotheses
Hazard fairness
Players will describe water pressure as fair because it advances only when they act.
Success threshold: at least half of testers explicitly note that they had time to think.
Dock attribution
Players will describe dock losses as their mistake, not as randomness.
Success threshold: fewer than 20% of overflow losses are described as “unlucky.”
Rescue-order readability
By Level 3, most testers will identify rescue order as the core decision.
Success threshold: at least 60% say some version of “I saved the wrong one first.”
Rescue framing
Players will describe the game as “save the animal before danger gets there,” not just sorting.
Success threshold: matches scope-lock target of 60%+.
Emotional landing
Even with greybox art, extraction plus Mae plus aftercare should create a distinct payoff.
Success threshold: testers mention the puppy, rescue, or relief beat without prompting.
Not a pseudo-timer
Players should feel pressure without describing the game as timed.
Success threshold: “timed” language appears in under 25% of first-session interviews.
4.2 Level-specific hypotheses
L1: Players call the target a puppy or “the dog,” not “the objective.”
L2: Players understand the dock is dangerous before the level ends.
L3: Players describe failure as wrong rescue order.
L4: Players understand ice after one exposure without needing text.
L7: Players see vine as a route issue, not random clutter.
L8: Most players notice vine preview before the first growth.
L10: Mixed blocker boards still read as one problem, not three systems.
L12: Three-target levels create triage, not overload.
L15: Cold viewers of recorded footage can explain the rescue stakes inside 10 seconds.
4.3 Questions to ask after play

Ask these verbatim.

Why did you lose that level?
What was the most important decision on Level 3?
Did the water feel fair or annoying?
Did you ever feel rushed while thinking?
What was the dock asking you to pay attention to?
Did saving the puppy feel different from just finishing a level?
What, if anything, felt random?
When vine grew, did it feel warned or arbitrary?
What do you think this game is, in one sentence?
Would you show this to someone as a rescue game or as a sorting game?
4.4 Telemetry to log for every level

Minimum useful instrumentation:

level start
level win / loss
loss cause: dock / water
action count
water rises count
dock occupancy per action
undo used y/n
target extraction order
first target extracted at action #
vine growth count
time spent idle between actions
invalid taps count

Without these, post-test interpretation will be too hand-wavy.

5. Post-test issue taxonomy

This is the triage framework I would use after first external playtests.

5.1 Unfair

What players say

“That was bullshit.”
“I had no chance.”
“It got me even though I knew what to do.”

Likely causes

water interval too fast
warning too weak or too late
extraction resolves after hazard by mistake
emergency spawn help too low
vine growth preview unreadable

Primary fix order

confirm pipeline ordering
improve warning clarity
slow water by 1 action band
increase spawn help before changing level layout
5.2 Unreadable

What players say

“I didn’t know what mattered.”
“There was too much going on.”
“I couldn’t tell which dog to save first.”

Likely causes

target distance differences too subtle
urgent lane not visually privileged enough
blocker mix introduced too quickly
too many debris types too early
dock warning state under-signaled

Primary fix order

simplify first read
strengthen target portrait / path emphasis
reduce piece variety
reduce simultaneous blocker density
5.3 Emotionally flat

What players say

“Cute, but I was mostly just matching.”
“Winning felt like finishing a puzzle, not a rescue.”

Likely causes

target too visually passive
extraction animation too short or generic
Mae absent at wrong moment
no pre-rescue concern audio or relief beat
aftercare shot too weak

Primary fix order

strengthen extraction and aftercare
strengthen target state change near rescue
strengthen Mae cameo timing
do not add systems; add affective clarity
5.4 Too generic

What players say

“This is tray sort with dogs.”
“The theme could be anything.”

Likely causes

rescue order does not bite by Level 3
too many boards are really about dock only
water feels background instead of deciding target priority
rescue extraction not sufficiently distinct

Primary fix order

sharpen Level 3 and Level 5 order tests
reduce pure dock challenge boards after Level 2
increase target-priority asymmetry
upgrade rescue presentation
5.5 Too punishing

What players say

“I understood it, but it was exhausting.”
“I got punished for every small mistake.”

Likely causes

assistance taper too aggressive
water too high too early
vine threshold too short
too many multi-target levels in a row
immediate fail cadence too dense

Primary fix order

increase assistance
lower start flood
relax vine threshold
spread heavy boards apart
5.6 Too easy to brute-force

What players say

“I just kept clearing stuff and won.”
“I didn’t really need a plan.”

Likely causes

water too slow
targets too close to extraction by default
dock clears too easy due to color distribution
wrong-order consequences too soft

Primary fix order

raise urgency via start flood before speeding water
separate target priorities more clearly
reduce obvious dock-fed colors
add one more required pivot in route
5.7 Special red-flag reads

These are kill/pause indicators, not just balance notes.

Red flag

“It’s basically a sorter with animals.”

Interpretation

Seed not landing. Stop adding features.

Red flag

“I was fighting the rack, not trying to rescue.”

Interpretation

Dock overshadowed rescue-order puzzle.

Red flag

“I knew the answer, but the game didn’t let me do it.”

Interpretation

Fairness failure. Probably action order, spawn support, or warning clarity.

These align directly with the scope-lock pause/kill criteria.

6. Final recommendation

For Phase 1, do not chase breadth. Chase clarity.

The strongest version of this prototype is:

15 levels
one water hazard
crate / ice / vine
7-slot dock
one puppy target family
one Mae state
one strong extraction beat
one brutally clear Level 3 rescue-order proof
one captureable Level 15 ad moment

If this version does not read as Rescue Grid, adding more systems will not save it. It will only hide the result.
