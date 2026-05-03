# First 100 Pacing Model

## Purpose

This file governs tension, release, confidence, difficulty ramp, and retention flow across the first 100 production levels.

`FIRST_100_ROADMAP.md` defines the topic arc: which major rescue, dock, water, blocker, and triage ideas each band should emphasize. This file defines the pacing and rhythm inside that arc: when to teach, when to pressure, when to recover, and when to test.

Exact level JSON still needs authoring, validation, preview, and solve verification. This model guides level-set sequencing; it does not replace board design, validator checks, preview review, or solve proof.

## Core retention principle

The first 100 should not be a straight difficulty ramp.

Use this core loop:

learn → succeed → choose → feel pressure → recover → feel clever → get tested → get rewarded

Players should not feel punished before they feel competent. Hard levels should make players want to retry because they saw the mistake, not because they feel tricked. Release levels are not filler; they are what make later pressure feel fair. A level can be easy and still valuable if it strengthens the game's rescue identity or the player's confidence.

## Default 10-level band rhythm

Use this as the default pattern for each 10-level band:

| Slot | Role | Purpose |
|---:|---|---|
| x1 | Teach / Reintroduce | Introduce or refresh the band's main idea safely |
| x2 | Practice | Let the player succeed with the idea in a real board |
| x3 | Choice | Add route or target comparison |
| x4 | Pressure | Add urgency through water, dock, vine, or blocker placement |
| x5 | Release | Give a satisfying rescue after pressure |
| x6 | Variation | Same skill in different geometry or board shape |
| x7 | Pressure Choice | Harder tradeoff, still readable |
| x8 | Exam | Combine recent skills without adding new confusion |
| x9 | Spectacle / Recovery | Memorable rescue beat or confidence rebuild |
| x10 | Capstone | Fair challenge proving the band's skill |

This is a default rhythm, not a rigid law. Deviations must be intentional and documented in level-set notes or `meta.notes`.

Do not run three pressure/exam levels in a row. Do not introduce a new mechanic and immediately test it at full pressure.

## Difficulty knobs in preferred order

1. Sharpen rescue-order pressure.
2. Tighten dock residue choices.
3. Adjust blocker placement.
4. Adjust target placement relative to water.
5. Increase route pivot demand.
6. Reduce assistance.
7. Increase starting flood.
8. Reduce water interval within allowed band.
9. Increase board size.
10. Increase target count.
11. Add mechanic combinations.

Do not use clutter as the first difficulty knob. Do not reduce readability to create difficulty. Do not rely on spawn luck to create challenge.

## Anti-churn rules

- No three hard levels in a row.
- No back-to-back unclear failures.
- No repeated dock-overflow frustration immediately after dock is introduced.
- No water loss before the player has clearly learned action-based water.
- No vine growth punishment before vine preview is understood.
- No three-target triage before two-target rescue order is stable.
- No exam level should introduce a new rule.
- Every hard exam should be followed by release, recovery, or spectacle.
- Every 10-level band should include at least one level designed to make the player feel powerful.

## Success-rate targets by role

| Role | Target first-attempt win rate |
|---|---:|
| Teach | 85–95% |
| Practice | 75–90% |
| Release | 80–95% |
| Recovery | 80–95% |
| Spectacle | 70–90% |
| Choice | 65–80% |
| Pressure | 55–75% |
| Exam | 40–65% |
| Capstone | 45–70% depending on band |
| Late hard exam | 30–50% |

These are design targets, not validator rules. First-attempt loss is acceptable only when attribution is clear. A player should usually know what to try differently.

## Emotional beat requirements

Every 10-level band should include:

- one first-clear "I get it" moment,
- one dock-risk moment,
- one rescue-order realization,
- one close-call rescue,
- one confidence/release level,
- one memorable rescue beat.

The player should remember rescues, not just board clearing. The level should end emotionally on extraction whenever possible.

## Chapter-specific pacing notes

### Levels 1–10: Rescue literacy

- More confidence than punishment.
- Teach rescue identity before complexity.
- Dock/water should be present but not dominate.
- L10 should feel like "I understand the core game," not "the game became hard."

### Levels 11–20: Dock discipline

- Dock failure may appear, but the dock must serve rescue.
- Include release levels where good dock discipline enables satisfying rescue.
- Avoid making the player feel the dock is the whole game.

### Levels 21–30: Ice and future value

- Teach ice investment before pressuring it.
- Include payoff levels where breaking ice feels clever.
- Avoid hidden-value confusion.

### Levels 31–40: Water rescue order

- Water should sharpen priority, not feel like a timer.
- Use target placement and starting flood before faster intervals.
- Include at least one close but fair last-action rescue.

### Levels 41–50: Vine pressure

- Vine must be warned and attributable.
- Teach static/blocking vine before growth pressure.
- Include recovery after vine punishment.
- Avoid vine becoming the whole level.

### Levels 51–60: Mixed blockers

- Combine systems, but keep one primary question per level.
- Use release levels after dense blocker exams.
- Avoid difficulty by clutter.

### Levels 61–70: Multi-target triage

- Not every level should have 3 targets.
- Use two-target boards to teach sharper order before broad triage.
- Three-target levels need clear order logic, not simultaneous chores.

### Levels 71–80: Dock-rescue integration

- Dock and rescue decisions should become one decision.
- Use costly-but-urgent route clears.
- Include final-rescue exception clarity as a positive clutch beat.

### Levels 81–90: Mastery remixes

- Remix without adding major new rules.
- Use unusual shapes and route pivots.
- Maintain recovery beats so mastery does not become fatigue.

### Levels 91–100: Campaign climax

- Alternate hard-clean challenges with emotional rescue payoff.
- L100 should be memorable and emotionally clear, not merely the hardest level.
- The final band should prove the game's identity: calm urgency, rescue order, self-authored dock pressure, satisfying extraction.

## Level-set generation requirements

When generating any set of levels, Codex must define before writing JSON:

- level id,
- role,
- primary skill,
- secondary skill,
- intended tension beat,
- intended release beat,
- expected path,
- expected fail mode,
- target first-attempt win-rate band,
- reason this level belongs after the previous one.

## Review checklist

Before accepting a generated set, check:

- Does the set have a tension/release curve?
- Are there too many pressure/exam levels in a row?
- Does each new idea get teach + practice before pressure?
- Is there a release after hard failure?
- Do levels escalate through decision quality rather than clutter?
- Does each band include at least one memorable rescue beat?
- Would a player want to continue after level 5, 10, 15, and 20?
