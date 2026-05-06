# Level Briefs

Level briefs are design contracts, not runtime content. They document the intended role, skill focus, mechanics, tuning band, tension beat, release beat, expected path, and expected fail mode for each authored Rescue Grid level.

The playable level JSON in `Assets/StreamingAssets/Levels/` must satisfy its matching brief. If a level intentionally changes design direction, Codex must update the relevant brief before or alongside the level JSON change so the contract and authored content stay aligned.

Level brief `targetFirstAttemptWinRate` values must match the bands in `.agents/skills/level-authoring/references/DIFFICULTY_TARGETS.md` unless the brief documents a deliberate exception.

These files are not loaded by the game runtime and should not be treated as executable level content.
