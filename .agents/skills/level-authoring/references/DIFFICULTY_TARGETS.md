# Difficulty Targets

## Purpose

This file defines first-pass difficulty targets for Rescue Grid level authoring, review, telemetry interpretation, and tuning.

These targets are design guardrails, not hard validator rules.

They help answer:

- Is this level too punishing for its role?
- Is this level too easy to teach anything?
- Is failure attributable?
- Does the level reward rescue-first play instead of generic clearing?
- Does the level fit the surrounding campaign pacing?

Difficulty targets do not replace human playtest. Bot telemetry is useful for relative signals, not exact human win prediction.

## Difficulty philosophy

Rescue Grid difficulty should come from readable decisions, not from clutter, hidden information, or real-time pressure.

Preferred difficulty sources:

1. Rescue-order priority.
2. Dock residue tradeoffs.
3. Blocker placement.
4. Target placement relative to water.
5. Route pivot demand.
6. Assistance reduction.
7. Starting flood height.
8. Water interval tightening within the allowed band.
9. Board size increase.
10. Target count increase.
11. Mixed-system combinations.

Do not make levels harder by:

- reducing readability,
- adding meaningless clutter,
- depending on lucky spawn,
- hiding the intended route,
- creating repeated dock overflow without attribution,
- making water feel like a real-time timer.

## Role-based human win-rate targets

Use these as target first-attempt human win-rate bands:

| Role | Target first-attempt human win rate | Design intent |
|---|---:|---|
| rule_teach | 90-98% | Teach the rule with minimal punishment |
| teach | 85-95% | Introduce a rule or mechanic safely |
| practice | 75-90% | Let the player apply a known idea |
| release | 80-95% | Restore confidence after pressure |
| recovery | 80-95% | Rebuild confidence after a heavy level |
| spectacle | 70-90% | Deliver a memorable rescue beat |
| choice | 65-80% | Make the player compare routes or priorities |
| pressure | 55-75% | Add urgency to a known skill |
| exam | 40-65% | Test learned skills without new rules |
| capstone | 45-70% | End a band with a fair challenge |
| late_hard_exam | 30-50% | Optional late-campaign mastery challenge |

Rules:

- Early onboarding should skew toward the high end of each range.
- A level may be below target only if it is explicitly an exam, capstone, or late hard exam.
- A level may be above target if it is a release, recovery, spectacle, or confidence-building level.
- First-attempt loss is acceptable only when the player can explain what to try differently.

## Bot telemetry targets

Offline bots are not human players. Use them as comparative diagnostic tools.

Recommended bot policies:

- random_legal
- greedy_clear
- rescue_focused
- dock_safe

### Bot target table

| Role | Rescue-focused bot win rate | Greedy-clear bot win rate | Random-legal bot win rate | Dock-safe bot win rate |
|---|---:|---:|---:|---:|
| rule_teach | 95-100% | 80-100% | 50-90% | 80-100% |
| teach | 90-100% | 65-90% | 35-75% | 75-95% |
| practice | 85-100% | 55-85% | 25-65% | 70-95% |
| release | 90-100% | 60-90% | 30-75% | 75-95% |
| recovery | 90-100% | 60-90% | 30-75% | 75-95% |
| spectacle | 80-100% | 50-85% | 25-65% | 65-95% |
| choice | 75-95% | 40-75% | 15-50% | 60-90% |
| pressure | 70-90% | 30-65% | 10-40% | 55-85% |
| exam | 60-85% | 20-55% | 5-30% | 45-75% |
| capstone | 65-90% | 25-60% | 5-35% | 50-80% |
| late_hard_exam | 45-75% | 10-40% | 0-20% | 35-65% |

Interpretation:

- If random_legal wins too often, the level may be too loose or self-solving.
- If rescue_focused loses too often, the level may be unfair, over-tightened, or spawn-dependent.
- If greedy_clear beats rescue_focused, the level may reward generic clearing over rescue-first play.
- If dock_safe wins but rescue_focused loses, dock pressure may be dominating rescue.
- If all bots lose heavily, the level may be too constrained or broken.
- If all bots win easily, the level may need sharper route priority or dock tradeoff unless its role is release/recovery.

## Loss reason targets

Track loss reason distribution where telemetry supports it.

Important loss reasons:

- LossDockOverflow
- LossWater
- deadboard_or_no_meaningful_route
- invalid_or_stalled_play
- timeout_by_depth_limit
- other_terminal_loss

### Healthy loss attribution by role

#### rule_teach / teach

Expected:
- very low loss rate,
- losses should be rare and attributable.

Warning signs:
- more than 10% LossDockOverflow,
- more than 10% LossWater,
- any frequent no-route or unclear loss.

#### practice / release / recovery

Expected:
- most players should recover from small mistakes,
- losses should mostly come from repeated poor decisions.

Warning signs:
- one mistake causes immediate unrecoverable failure,
- dock overflow dominates,
- water loss occurs before the player can read the route.

#### choice / pressure

Expected:
- losses reveal the intended mistake:
  - wrong rescue order,
  - overfilled dock,
  - ignored vine,
  - spent too long on low-value route.

Warning signs:
- losses split evenly across unrelated causes,
- players could blame dock, water, vine, and spawn at once,
- common loss happens after target is visibly one-clear-away.

#### exam / capstone

Expected:
- higher loss rate is acceptable,
- dominant failure should still be explainable.

Warning signs:
- no dominant failure pattern,
- rescue-focused bot cannot find stable wins,
- level requires exact hidden sequence,
- greedy clearing wins more than intended route play.

## Onboarding-specific targets

Apply these stricter rules to L00-L15.

Current L00-L20 packet membership is governed by `docs/level-packets/phase1.packet.json`, and current L16-L20 intent is governed by `docs/level-briefs/`. Until a dedicated L16-L20 difficulty reference exists, interpret L16-L20 through their briefs, manifest role, telemetry, and packet pacing reports.

### L00

- Human first-attempt target: 90-98%.
- Rescue-focused bot should almost always win.
- Failure should not be the lesson.
- If players lose, the level is too punishing or unclear.

### L01-L03

- Human first-attempt target: 75-95% depending on role.
- Player should see target progress and extraction quickly.
- Dock pressure may appear, but should not dominate.
- Water should be visible but forgiving.
- No repeated early hard losses.

### L04-L06

- Human first-attempt target: 65-90%.
- Ice must be useful, not merely obstructive.
- Player should feel clever for using future value.
- L05 should function as practice/release, not a punishment spike.

### L07-L09

- Human first-attempt target: 60-90%.
- Vine must be readable before it becomes pressure.
- Vine growth must be warned and attributable.
- L09 can pressure the player, but should not make vine feel random.

### L10-L15

- Human first-attempt target: 40-80% depending on role.
- L10 and L14 may be exams.
- L11 should recover confidence after L10.
- L15 should be spectacle/capstone, not simply the hardest level.
- L15 should be readable quickly and emotionally clear.

## Anti-churn thresholds

Flag a level set for review if any of these occur:

- Three pressure/exam/capstone levels appear in a row.
- Two consecutive levels have unclear dominant failure reasons.
- A teach level has repeated dock overflow losses.
- A water lesson produces water losses before water is understood.
- Vine causes losses before vine preview has been taught.
- Three-target triage appears before two-target rescue order is stable.
- A level immediately after an exam is also high pressure with no release/recovery.
- A player could reasonably say “I lost but I do not know why.”
- A player could reasonably say “I opened the puppy and the game did not count it.”
- A player could reasonably say “It is just tray sort with puppies.”

## Telemetry review rules

When reviewing telemetry, answer:

1. Does rescue_focused perform better than greedy_clear?
2. Does dock_safe reveal that the dock is overpowering rescue?
3. Does random_legal win rate suggest the level is too loose?
4. Does the dominant loss reason match the expectedFailMode?
5. Does the median win action count fit the level’s role?
6. Do players/bots reach target progress and one-clear-away states before extraction?
7. Are losses happening before the level’s primary skill is expressed?
8. Does the level fit the pacing needs of surrounding levels?

## Softening rules

Soften a level when:

- rescue_focused bot is below the target range,
- the expected fail mode is not the dominant failure,
- dock overflow dominates on teach/practice/release levels,
- water loss dominates before rescue order is clear,
- failures occur before target progress is visible,
- the solve requires exact hidden sequencing,
- the board looks dense but not readable.

Preferred softening order:

1. Improve route readability.
2. Add recovery margin.
3. Improve target state progression visibility.
4. Reduce misleading side bait.
5. Improve dock-safe route options.
6. Move water risk farther from the target.
7. Increase rise interval within the band.
8. Increase assistance.
9. Reduce blocker count or blocker adjacency.
10. Reduce target count only if the level role allows it.

## Hardening rules

Harden a level when:

- random_legal wins too often,
- greedy_clear wins more than rescue_focused,
- the expected fail mode rarely appears,
- the intended choice is too obvious,
- the board has no meaningful dock tension,
- water is ignored on a water-priority level,
- vine is irrelevant on a vine-pressure level.

Preferred hardening order:

1. Sharpen the wrong-but-tempting route.
2. Tighten dock residue tradeoff.
3. Move the urgent target closer to water.
4. Add route pivot demand.
5. Add one meaningful blocker.
6. Reduce assistance.
7. Increase starting flood.
8. Reduce water interval within allowed band.
9. Increase board size only if readability remains strong.
10. Add another target only for triage/exam roles.

## Report format for level difficulty review

When Codex reviews a level or level set, use this format:

Level:
Role:
Target first-attempt range:
Expected fail mode:
Telemetry summary:
- rescue_focused:
- greedy_clear:
- dock_safe:
- random_legal:
Dominant loss reason:
Difficulty verdict:
- too_easy / on_target / too_hard / unclear
Design verdict:
- rewards_rescue / rewards_clearing / dock_dominant / water_dominant / unclear
Recommended change:
- none / soften / harden / rebuild
Reason:

## Definition of done

A level’s difficulty is acceptable when:

- its role matches its target range,
- rescue-focused play performs at or above target,
- generic clearing does not outperform rescue-first play,
- the dominant failure matches expectedFailMode,
- losses are attributable,
- the level fits surrounding pacing,
- the level has at least one satisfying rescue payoff,
- any deviation is documented in the level brief or meta.notes.
