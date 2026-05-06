# L11-L15 Telemetry Difficulty Audit

Generated from offline LevelTelemetry reports on 2026-05-06 with 200 samples per bot and a 50-action cap.

Bot telemetry is a relative design diagnostic, not a human win-rate prediction. Verdicts use the role targets in `.agents/skills/level-authoring/references/DIFFICULTY_TARGETS.md`.

## Summary

| Level | Role | Bot win rates (rescue / greedy / dock / random) | Dominant observed loss | Difficulty verdict | Design verdict | Recommended change |
|---|---|---:|---|---|---|---|
| L11 | recovery | 100% / 26% / 50% / 14% | Greedy loses mostly to rescue-path flooding, then dock overflow | on_target | rewards_rescue | none |
| L12 | choice | 100% / 3% / 4% / 11% | Greedy loses almost entirely to rescue-path flooding | on_target | rewards_rescue | none |
| L13 | pressure | 92% / 39% / 22% / 23% | Greedy loses to dock overflow and rescue-path flooding | on_target | rewards_rescue | none |
| L14 | exam | 99% / 3% / 0% / 2% | Greedy and dock-safe lose to rescue-path flooding | on_target | rewards_rescue | none |
| L15 | spectacle | Pending retest after tune | Pending retest after tune | pending | pending | pending |

## Level Notes

### L11

- Expected fail mode: player commits too long to the open target and loses tempo on the lower target.
- Telemetry: rescue-focused wins every run; greedy-clear wins only 26% and loses mostly to rescue-path flooding, which matches the wrong-order lesson.
- Verdict: on target for recovery. Rescue-first play is clearly rewarded while generic clearing is punished without making the intended route fragile.

### L12

- Expected fail mode: player half-solves all three and saves none efficiently.
- Telemetry: rescue-focused wins 100%; greedy-clear wins 3%; random-legal wins 11%.
- Verdict: on target for choice. The level strongly rewards triage and punishes generic clearing. Watch human playtests for readability, because dock-safe at 4% means defensive play alone is not a viable substitute for target order.

### L13

- Expected fail mode: player lets vine grow, then becomes action-starved or dock-stressed while rerouting.
- Telemetry: rescue-focused wins 92%; greedy-clear wins 39%; losses split between dock overflow and rescue-path flooding.
- Verdict: on target for pressure. Rescue-focused play outperforms clearing and stays above the pressure-role target, while failure still reads as combined route/tax pressure.

### L14

- Expected fail mode: player brute-force clears or hesitates and suffers dock or water failure.
- Telemetry: rescue-focused wins 99%; greedy-clear wins 3%; dock-safe wins 0%; random-legal wins 2%.
- Verdict: on target for exam, with a human-playtest watch item. The bot spread proves rescue commitment, but non-rescue policies are extremely weak, so the board must stay visually readable in play.

### L15

- Expected fail mode: player chases a tempting low-value side route and loses hero tempo.
- Pre-tune telemetry: rescue-focused and dock-safe both won 100%; greedy-clear won 78%; random-legal won 50%; rescue-focused median win was 2 actions.
- Pre-tune built-in signal: `no_target_progress_events_seen`.
- Tuning applied: preserve the hero sightline, change the lower hero rescue from a free B triple to a B pair, and add a readable right-side B follow-up so the secondary puppy reaches one-clear-away before extraction.
- Verdict: pending retest after tune.
