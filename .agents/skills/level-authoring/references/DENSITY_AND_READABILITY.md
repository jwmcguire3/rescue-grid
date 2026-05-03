# Density and Readability

## Required authoring discipline

Every level must have clear `meta`.

Every board should begin visually complete by default. The opening should look like real gameplay, not a sparse diagram explaining the lesson.

Empty cells must be justified by one of these purposes:

- authored geometry,
- environmental negative space,
- hazard space,
- spawn corridor,
- deliberate rescue route,
- mobile readability.

Do not use empty cells merely to point at the lesson.

If density is unusually low, explain why in `meta.notes`.

## Start-state density

The start state should invite play without looking pre-solved.

Good density:

- gives the player several legal groups,
- keeps the rescue route visible but not already open,
- provides dock-relevant choices,
- frames blockers as route problems,
- leaves water and target danger readable.

Bad density:

- creates a tutorial diagram instead of a level,
- exposes the intended path by deleting most of the board,
- leaves large empty regions with no authored purpose,
- makes gravity/spawn solve the route,
- hides the target in clutter.

## First-move readability

Early levels should have a legible first move without deleting most of the board.

Guide the player through:

- local clusters,
- route framing,
- target placement,
- blocker placement,
- water forecast,
- dock setup,
- color/type distribution.

Hard levels may offer several plausible first moves, but they should not be visually mushy.

## Mobile readability

A level that is readable in editor preview may still fail on mobile. Watch for:

- dense mixed multi-character tile codes near targets,
- too many blockers around the first target,
- target routes hidden in diagonal visual noise,
- vine and ice competing for attention,
- tiny islands of singletons that look tappable but are not useful.

Use negative space sparingly to clarify rescue lanes or hazard space, not to flatten the puzzle.

## Rescue route readability

The player should be able to form a hypothesis about the route before acting.

Readable routes usually have:

- target-adjacent blockers that explain the route,
- a visible urgent side and safe side,
- at least one satisfying route-opening move,
- enough surrounding debris to make dock consequences real,
- enough margin that the expected path survives normal play.

Unreadable routes usually have:

- equally noisy options everywhere,
- blockers that do not imply a lane,
- target access hidden behind unrelated clutter,
- a solution that only appears after lucky spawn,
- no visible reason one target should come first.

## Final review checklist

Before finalizing a level, confirm:

- `meta` explains the design clearly enough for another designer to maintain it.
- The board looks visually complete.
- Empty cells are justified.
- The primary skill is required to win.
- The first move is readable for the level's place in the campaign.
- Rescue order matters when the level claims it matters.
- Dock pressure comes from player choices, not raw clutter.
- Water and vine pressure are forecasted and attributable.
- The expected path works without hidden spawn luck.
- The expected fail mode is fair and teachable.
