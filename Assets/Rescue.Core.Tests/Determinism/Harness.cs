using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rescue.Core.Tests.Determinism
{
    public static class DeterminismHarness
    {
        public static IReadOnlyList<TState> RecordStateStream<TState, TInput>(
            uint seed,
            IReadOnlyList<TInput> inputs,
            Func<TState, TInput, TState> stepFn,
            Func<uint, TState> initialStateFromSeed)
        {
            if (inputs is null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (stepFn is null)
            {
                throw new ArgumentNullException(nameof(stepFn));
            }

            if (initialStateFromSeed is null)
            {
                throw new ArgumentNullException(nameof(initialStateFromSeed));
            }

            List<TState> states = new List<TState>(inputs.Count + 1);
            TState current = initialStateFromSeed(seed);
            states.Add(current);

            for (int i = 0; i < inputs.Count; i++)
            {
                current = stepFn(current, inputs[i]);
                states.Add(current);
            }

            return states.AsReadOnly();
        }

        public static void AssertIdenticalRuns<TState, TInput>(
            uint seed,
            IReadOnlyList<TInput> inputs,
            Func<TState, TInput, TState> stepFn,
            Func<uint, TState> initialStateFromSeed)
            where TState : IEquatable<TState>
        {
            IReadOnlyList<TState> firstRun = RecordStateStream(seed, inputs, stepFn, initialStateFromSeed);
            IReadOnlyList<TState> secondRun = RecordStateStream(seed, inputs, stepFn, initialStateFromSeed);

            if (firstRun.Count != secondRun.Count)
            {
                throw new AssertionException($"Determinism mismatch: run lengths differ ({firstRun.Count} vs {secondRun.Count}).");
            }

            EqualityComparer<TState> comparer = EqualityComparer<TState>.Default;
            for (int i = 0; i < firstRun.Count; i++)
            {
                TState first = firstRun[i];
                TState second = secondRun[i];

                if (!comparer.Equals(first, second))
                {
                    string label = i == 0 ? "initial state" : $"step {i}";
                    throw new AssertionException(
                        $"Determinism mismatch at {label}.{Environment.NewLine}" +
                        $"Expected: {first}{Environment.NewLine}" +
                        $"Actual:   {second}");
                }
            }
        }
    }
}
