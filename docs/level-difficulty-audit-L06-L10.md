# L06-L10 Difficulty Audit

Telemetry source:

- `Reports/LevelTelemetry/L06-L10-Baseline/`
- `Reports/LevelTelemetry/L06-L10-Confidence/`

Confidence run settings:

- Original samples per bot: 500
- Original max actions: 40
- Bots: `random_legal`, `greedy_clear`, `rescue_focused`, `dock_safe`

Retune follow-up:

- `Reports/LevelTelemetry/L06-L08-Tuned-Final-Confidence/`
- `Reports/LevelTelemetry/L06-Tuned-SecondPass-Confidence/`
- Samples per bot: 500
- Max actions: 40

Bot telemetry is a comparative diagnostic, not a direct human win-rate prediction.

## Summary

| Level | Role | Rescue-focused | Greedy-clear | Dock-safe | Random-legal | Verdict |
|---|---:|---:|---:|---:|---:|---|
| L06 | choice | 77.2% | 22.8% | 52.6% | 13.0% | On target |
| L07 | teach | 100.0% | 100.0% | 100.0% | 81.4% | Retuned easy teach |
| L08 | practice | 100.0% | 58.8% | 91.4% | 73.4% | Retuned very easy |
| L09 | pressure | 65.2% | 25.2% | 36.6% | 31.6% | On target, monitor random win rate |
| L10 | exam | 73.6% | 10.2% | 55.6% | 19.8% | On target |

## L06

Role: `choice`

Target first-attempt range: 65-80%

Expected fail mode: Player takes satisfying broad side clears and wastes the action budget.

Telemetry summary:

- `rescue_focused`: 77.2% win rate, median win at 7 actions
- `greedy_clear`: 22.8% win rate
- `dock_safe`: 52.6% win rate
- `random_legal`: 13.0% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: on_target

Design verdict: rewards_rescue

Recommended change: none

Reason: Second-pass tuning opened one additional lower route side, letting the first puppy extract earlier and reducing route residue. Rescue-focused play is now inside the `choice` bot target, generic clearing remains meaningfully lower, and the telemetry tool emits no difficulty signals for L06.

## L07

Role: `teach`

Target first-attempt range: 75-90%

Expected fail mode: Player treats vine as background clutter and detours too slowly.

Telemetry summary:

- `rescue_focused`: 100.0% win rate, median win at 1 action
- `greedy_clear`: 100.0% win rate
- `dock_safe`: 100.0% win rate
- `random_legal`: 81.4% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: very_easy

Design verdict: confidence_teach

Recommended change: accept as teach or rebuild if a multi-action vine intro is desired

Reason: Tuning removed unintended pre-flooding and made the intended lower vine-lane clear a clean triple. The level now consistently teaches the static vine lane, but it is no longer a difficulty check.

## L08

Role: `practice`

Target first-attempt range: 70-85%

Expected fail mode: Player ignores the preview and lets the route become slower.

Telemetry summary:

- `rescue_focused`: 100.0% win rate, median win at 1 action
- `greedy_clear`: 58.8% win rate
- `dock_safe`: 91.4% win rate
- `random_legal`: 73.4% win rate

Dominant rescue-focused terminal reason: `Win`

Difficulty verdict: very_easy

Design verdict: confidence_practice

Recommended change: accept as confidence practice or rebuild if vine growth must be observed before success

Reason: Tuning removed unintended pre-flooding, increased water margin and assistance, and opened the right rescue side. Rescue-focused play now reliably wins, but random and dock-safe policies are also high, so the level is now a forgiving practice beat rather than a pressure tutorial.

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
