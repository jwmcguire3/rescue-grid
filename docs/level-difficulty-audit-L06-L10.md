# L06-L10 Difficulty Audit

Telemetry source:

- `Reports/LevelTelemetry/L06-L10-Baseline/`
- `Reports/LevelTelemetry/L06-L10-Confidence/`

Confidence run settings:

- Samples per bot: 500
- Max actions: 40
- Bots: `random_legal`, `greedy_clear`, `rescue_focused`, `dock_safe`

Bot telemetry is a comparative diagnostic, not a direct human win-rate prediction.

## Summary

| Level | Role | Rescue-focused | Greedy-clear | Dock-safe | Random-legal | Verdict |
|---|---:|---:|---:|---:|---:|---|
| L06 | choice | 30.0% | 5.6% | 9.8% | 0.8% | Too hard |
| L07 | teach | 38.0% | 82.8% | 29.2% | 24.0% | Rebuild or retune |
| L08 | practice | 53.0% | 47.4% | 21.0% | 12.4% | Too hard |
| L09 | pressure | 65.2% | 25.2% | 36.6% | 31.6% | On target, monitor random win rate |
| L10 | exam | 73.6% | 10.2% | 55.6% | 19.8% | On target |

## L06

Role: `choice`

Target first-attempt range: 65-80%

Expected fail mode: Player takes satisfying broad side clears and wastes the action budget.

Telemetry summary:

- `rescue_focused`: 30.0% win rate, median win at 12 actions
- `greedy_clear`: 5.6% win rate
- `dock_safe`: 9.8% win rate
- `random_legal`: 0.8% win rate

Dominant rescue-focused terminal reason: `LossRescuePathFlooded`

Difficulty verdict: too_hard

Design verdict: rewards_rescue, but over-tightened

Recommended change: soften

Reason: Rescue-focused play strongly outperforms generic clearing, so the level is testing the right idea, but its win rate is far below the `choice` bot target. The dominant failure is water reaching the rescue path, which suggests the central route needs more recovery margin or clearer/faster access.

## L07

Role: `teach`

Target first-attempt range: 75-90%

Expected fail mode: Player treats vine as background clutter and detours too slowly.

Telemetry summary:

- `rescue_focused`: 38.0% win rate, median win at 5 actions
- `greedy_clear`: 82.8% win rate
- `dock_safe`: 29.2% win rate
- `random_legal`: 24.0% win rate

Dominant rescue-focused terminal reason: `LossRescuePathFlooded`

Difficulty verdict: too_hard

Design verdict: rewards_clearing

Recommended change: rebuild or retune

Reason: Greedy clearing massively outperforms rescue-focused play on a teach level. The committed golden path also solves in one action, so the authored intended path is too short while the bot field still loses often. That combination points to a level shape problem: the teach beat is not consistently expressed.

## L08

Role: `practice`

Target first-attempt range: 70-85%

Expected fail mode: Player ignores the preview and lets the route become slower.

Telemetry summary:

- `rescue_focused`: 53.0% win rate, median win at 6 actions
- `greedy_clear`: 47.4% win rate
- `dock_safe`: 21.0% win rate
- `random_legal`: 12.4% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: too_hard

Design verdict: rewards_rescue, but with tight water pressure

Recommended change: soften

Reason: Rescue-focused play beats greedy clearing, so the intended rule is present, but the win rate is below the `practice` target. Keep the vine preview beat, but add recovery margin before changing the core route.

## L09

Role: `pressure`

Target first-attempt range: 60-75%

Expected fail mode: Player tunnels on the vine-threatened upper side and loses the lower puppy or dock tempo.

Telemetry summary:

- `rescue_focused`: 65.2% win rate, median win at 10 actions
- `greedy_clear`: 25.2% win rate
- `dock_safe`: 36.6% win rate
- `random_legal`: 31.6% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: on_target

Design verdict: rewards_rescue

Recommended change: none for difficulty; monitor random win rate

Reason: Rescue-focused play is inside the pressure target band and clearly beats greedy and dock-safe policies. Random-legal is also inside the pressure target band, but high enough that future tuning should watch for the level becoming too loose.

## L10

Role: `exam`

Target first-attempt range: 50-70%

Expected fail mode: Player over-collects dock value or side clears before opening the choke.

Telemetry summary:

- `rescue_focused`: 73.6% win rate, median win at 4 actions
- `greedy_clear`: 10.2% win rate
- `dock_safe`: 55.6% win rate
- `random_legal`: 19.8% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: on_target

Design verdict: rewards_rescue

Recommended change: none

Reason: Rescue-focused play lands inside the exam bot target and decisively beats generic clearing. Dock-safe remains viable but does not overpower rescue-focused play.
