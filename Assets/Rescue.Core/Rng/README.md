# Rescue.Core RNG

`Rescue.Core.Rng.SeededRng` uses the 64-bit `xorshift64*` generator described by
Sebastiano Vigna in *An experimental exploration of Marsaglia's xorshift
generators, scrambled* (2014), built on George Marsaglia's xorshift family of
PRNGs. Reference: [https://arxiv.org/abs/1402.6246](https://arxiv.org/abs/1402.6246).

Rules:

- No caller in `Rescue.Core` may import randomness from anywhere other than
  `SeededRng`.
- `SeededRng` must remain free of engine dependencies, framework RNG helpers,
  static mutable state, and platform-specific entropy sources.

Why the state is stored as `uint`s:

- `RngState` is serialized as two explicit `uint` values so save/load data stays
  stable across platforms and does not depend on runtime-specific signed integer
  behavior.
- The generator's internal 64-bit state is reconstructed from those two `uint`
  halves, which makes bit shifts and persisted snapshots easier to reason about
  than signed `int` state.
- Using `uint` also avoids accidental sign extension when future systems inspect
  or restore deterministic snapshots.
