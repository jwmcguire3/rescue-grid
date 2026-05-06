# First-20 Level Review Pass

Generated on 2026-05-06 from `L00-L20` inclusive.

Evidence commands run:

- `dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- design-report-all Assets/StreamingAssets/Levels docs/level-briefs`
- `dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-golden`
- `dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-all`
- `dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- --range L00-L20 --samples 200 --max-actions 30`

All authored level files, briefs, solve files, and golden files are present. All golden paths verified as `Win -> PASS`. `L03` is the only level with a committed fail path, and it verifies as `LossDockOverflow -> PASS`.

Bot telemetry is a relative design diagnostic, not a human win-rate prediction. Decisions are intentionally conservative: when the design report recommends a missing fail path, the row is marked `tune` unless the level has separate fail-proof evidence.

| Level | Report status | Main warning | Golden | Fail path | Assistance risk | Decision |
|---|---|---|---|---|---|---|
| L00 | PASS | High assistance; bot signal `no_target_progress_events_seen` | pass | missing optional | high | tune - assistance proof |
| L01 | WARN: density above target, noted | High effective assistance and emergency dock assistance | pass | missing optional | high | tune - assistance proof |
| L02 | WARN: density above target, noted | Bot flags random_legal high win rate and dock overflow too prominent for role | pass | missing optional | low | tune - water/dock pressure |
| L03 | WARN: core hazard budget | High assistance and emergency water assistance; fail proof exists | pass | present/pass | high | tune - assistance/water-budget proof |
| L04 | PASS | Missing recommended fail path; high assistance | pass | missing recommended | high | tune - fail-path proof |
| L05 | PASS | Missing recommended fail path; rescue-focused bot win rate is soft for release role | pass | missing recommended | high | tune - assistance/difficulty proof |
| L06 | PASS | Missing recommended fail path; high assistance | pass | missing recommended | high | tune - assistance proof |
| L07 | WARN: density below target, noted | Missing recommended fail path and below-target density | pass | missing recommended | none | tune - density/readability proof |
| L08 | WARN: density below target, noted | Missing recommended fail path; random_legal high win rate; no target progress events | pass | missing recommended | medium | tune - density/readability and assistance proof |
| L09 | PASS | Missing recommended fail path; bot flags many runs reaching max actions | pass | missing recommended | medium | tune - fail-path and water/dock pressure proof |
| L10 | PASS | Missing recommended fail path | pass | missing recommended | medium | tune - fail-path proof |
| L11 | PASS | Missing recommended fail path | pass | missing recommended | medium | tune - fail-path proof |
| L12 | PASS | Missing recommended fail path | pass | missing recommended | medium | tune - fail-path proof |
| L13 | PASS | Missing recommended fail path | pass | missing recommended | low | tune - fail-path proof |
| L14 | PASS | Missing recommended fail path; non-rescue bots are extremely weak | pass | missing recommended | low | tune - fail-path proof |
| L15 | PASS | Missing recommended fail path; bot signal `no_target_progress_events_seen` | pass | missing recommended | low | tune - fail-path proof |
| L16 | WARN: density above target, noted | Missing recommended fail path; full occupancy exception should remain visually checked | pass | missing recommended | low | tune - fail-path proof |
| L17 | WARN: density above target, noted | Missing recommended fail path; full occupancy pressure read should remain visually checked | pass | missing recommended | low | tune - fail-path proof |
| L18 | WARN: density above target, noted | Missing recommended fail path; full occupancy exam should remain visually checked | pass | missing recommended | low | tune - fail-path proof |
| L19 | WARN: density above target, noted | Missing recommended fail path; full occupancy spectacle exception should remain visually checked | pass | missing recommended | low | tune - fail-path proof |
| L20 | WARN: density above target, noted | Missing recommended fail path; capstone density exception should remain visually checked | pass | missing recommended | low | tune - fail-path proof |

## Follow-Up Groups

### Fail-path proof

Add or explicitly waive fail-path artifacts for `L04-L20`. Prioritize `L09-L14`, `L18`, and `L20` first because these are pressure, exam, choice, or capstone beats where loss attribution matters most.

### Assistance proof

Compare no-assistance or reduced-assistance solves for `L00`, `L01`, `L03`, `L04`, `L05`, `L06`, `L08`, `L09`, `L10`, `L11`, and `L12`. The goal is not to remove assistance; it is to prove assistance supports recovery instead of solving the authored route.

### Density/readability proof

Visually inspect preview/SVG output for `L01`, `L02`, `L07`, `L08`, and `L16-L20`. The current density exceptions are documented and validator-acceptable, but they should stay tied to a readable rescue purpose.

### Bot-balance watchlist

Review `L02`, `L05`, `L06`, and `L09` before editing other levels. These have the clearest telemetry warnings: random/legal looseness, release-role softness, high assistance, or too many max-action runs.

## Current Verdict

No level currently needs `rebuild` or `cut` based on the automated evidence. The set is valid, golden-proven, and aligned with the Phase 1 identity at a structural level. The first pass should become a tuning/proof pass rather than a content replacement pass.
