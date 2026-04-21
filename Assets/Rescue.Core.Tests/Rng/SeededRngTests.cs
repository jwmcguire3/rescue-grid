using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.Tests.Determinism;

namespace Rescue.Core.Tests.Rng
{
    public sealed class SeededRngTests
    {
        [Test]
        public void SameSeedProducesSameSequencesAcrossMethods()
        {
            AssertMethodSequenceMatches(
                seed: 123456789U,
                sampleCount: 10000,
                selector: rng => rng.NextInt(-250, 250));

            AssertMethodSequenceMatches(
                seed: 987654321U,
                sampleCount: 10000,
                selector: rng => rng.NextFloat());

            string[] pickItems = { "crate", "ice", "vine", "puppy" };
            AssertMethodSequenceMatches(
                seed: 42424242U,
                sampleCount: 10000,
                selector: rng => rng.Pick(pickItems));

            (string item, double weight)[] weightedItems =
            {
                ("crate", 1.0d),
                ("ice", 2.0d),
                ("vine", 3.0d),
            };

            AssertMethodSequenceMatches(
                seed: 31415926U,
                sampleCount: 10000,
                selector: rng => rng.WeightedPick(weightedItems));
        }

        [Test]
        public void CloneProducesIndependentStreamWithoutAdvancingParent()
        {
            SeededRng parent = new SeededRng(777U);
            for (int i = 0; i < 32; i++)
            {
                _ = parent.NextInt(0, 1000);
            }

            SeededRng clone = parent.Clone();

            for (int i = 0; i < 500; i++)
            {
                int cloneValue = clone.NextInt(-5000, 5000);
                int parentValue = parent.NextInt(-5000, 5000);
                Assert.That(cloneValue, Is.EqualTo(parentValue), "Streams diverged at sample " + i + ".");
            }
        }

        [Test]
        public void StateRoundTripContinuesSequenceFromSavedPoint()
        {
            SeededRng original = new SeededRng("midstream-state");
            for (int i = 0; i < 73; i++)
            {
                _ = original.NextInt(0, 10000);
            }

            RngState savedState = original.GetState();
            SeededRng restored = SeededRng.FromState(savedState);

            for (int i = 0; i < 1000; i++)
            {
                float originalValue = original.NextFloat();
                float restoredValue = restored.NextFloat();
                Assert.That(restoredValue, Is.EqualTo(originalValue), "Restored stream diverged at sample " + i + ".");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            SeededRng first = new SeededRng(1U);
            SeededRng second = new SeededRng(2U);

            bool foundDifference = false;
            for (int i = 0; i < 1000; i++)
            {
                if (first.NextInt(0, 1000000) != second.NextInt(0, 1000000))
                {
                    foundDifference = true;
                    break;
                }
            }

            Assert.That(foundDifference, Is.True, "Different seeds unexpectedly produced identical samples.");
        }

        [Test]
        public void WeightedPickMatchesExpectedFrequenciesWithinTolerance()
        {
            SeededRng rng = new SeededRng(20260421U);
            (string item, double weight)[] items =
            {
                ("crate", 1.0d),
                ("ice", 3.0d),
                ("vine", 6.0d),
            };

            Dictionary<string, int> counts = new Dictionary<string, int>
            {
                ["crate"] = 0,
                ["ice"] = 0,
                ["vine"] = 0,
            };

            const int sampleCount = 100000;
            for (int i = 0; i < sampleCount; i++)
            {
                string item = rng.WeightedPick(items);
                counts[item]++;
            }

            AssertFrequency(counts["crate"], sampleCount, 0.1d, 0.02d, "crate");
            AssertFrequency(counts["ice"], sampleCount, 0.3d, 0.02d, "ice");
            AssertFrequency(counts["vine"], sampleCount, 0.6d, 0.02d, "vine");
        }

        [Test]
        public void DeterminismHarnessProducesIdenticalRuns()
        {
            int[] inputs = { 1, 2, 3, 4, 5, 6, 7 };

            DeterminismHarness.AssertIdenticalRuns(
                99U,
                inputs,
                (state, input) => state + input,
                seed => (int)seed);
        }

        private static void AssertMethodSequenceMatches<T>(uint seed, int sampleCount, Func<SeededRng, T> selector)
        {
            SeededRng first = new SeededRng(seed);
            SeededRng second = new SeededRng(seed);

            for (int i = 0; i < sampleCount; i++)
            {
                T firstValue = selector(first);
                T secondValue = selector(second);
                Assert.That(secondValue, Is.EqualTo(firstValue), "Sequences diverged at sample " + i + ".");
            }
        }

        private static void AssertFrequency(int observedCount, int sampleCount, double expectedFrequency, double tolerance, string label)
        {
            double actualFrequency = observedCount / (double)sampleCount;
            double delta = Math.Abs(actualFrequency - expectedFrequency);
            Assert.That(
                delta,
                Is.LessThanOrEqualTo(tolerance),
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} frequency was {1:P2}, expected {2:P2} within {3:P2}.",
                    label,
                    actualFrequency,
                    expectedFrequency,
                    tolerance));
        }
    }
}
