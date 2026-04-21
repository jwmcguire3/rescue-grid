using System;
using System.Collections.Generic;

namespace Rescue.Core.Rng
{
    public readonly record struct RngState(uint S0, uint S1);

    public sealed class SeededRng
    {
        private const ulong ZeroStateFallback = 0x9E3779B97F4A7C15UL;
        private const uint FnvOffsetBasis = 2166136261U;
        private const uint FnvPrime = 16777619U;

        private uint _s0;
        private uint _s1;

        public SeededRng(uint seed)
            : this(ExpandSeed(seed))
        {
        }

        public SeededRng(string seedString)
            : this(HashSeedString(seedString))
        {
        }

        private SeededRng(ulong state)
        {
            SetState(state);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            long span = (long)maxExclusive - minInclusive;
            if (span <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            ulong bound = (ulong)span;
            ulong threshold = (0UL - bound) % bound;

            while (true)
            {
                ulong sample = NextUInt64();
                if (sample < threshold)
                {
                    continue;
                }

                return (int)(minInclusive + (long)(sample % bound));
            }
        }

        public float NextFloat()
        {
            uint sample = (uint)(NextUInt64() >> 40);
            return sample * (1.0f / 16777216.0f);
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("Pick requires at least one item.", nameof(items));
            }

            return items[NextInt(0, items.Count)];
        }

        public T WeightedPick<T>(IReadOnlyList<(T item, double weight)> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("WeightedPick requires at least one item.", nameof(items));
            }

            double totalWeight = 0.0d;
            for (int i = 0; i < items.Count; i++)
            {
                double weight = items[i].weight;
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0d)
                {
                    throw new ArgumentOutOfRangeException(nameof(items), "Weights must be finite and non-negative.");
                }

                totalWeight += weight;
            }

            if (totalWeight <= 0.0d)
            {
                throw new ArgumentException("WeightedPick requires at least one positive weight.", nameof(items));
            }

            double target = NextUnitDouble() * totalWeight;
            double cumulativeWeight = 0.0d;
            int fallbackIndex = -1;

            for (int i = 0; i < items.Count; i++)
            {
                double weight = items[i].weight;
                if (weight <= 0.0d)
                {
                    continue;
                }

                fallbackIndex = i;
                cumulativeWeight += weight;
                if (target < cumulativeWeight)
                {
                    return items[i].item;
                }
            }

            return items[fallbackIndex].item;
        }

        public SeededRng Clone()
        {
            return FromState(GetState());
        }

        public RngState GetState()
        {
            return new RngState(_s0, _s1);
        }

        public static SeededRng FromState(RngState state)
        {
            return new SeededRng(ToStateValue(state));
        }

        private static uint HashSeedString(string seedString)
        {
            if (seedString is null)
            {
                throw new ArgumentNullException(nameof(seedString));
            }

            uint hash = FnvOffsetBasis;
            for (int i = 0; i < seedString.Length; i++)
            {
                char character = seedString[i];

                hash ^= (byte)character;
                hash *= FnvPrime;

                hash ^= (byte)(character >> 8);
                hash *= FnvPrime;
            }

            return hash;
        }

        private static ulong ExpandSeed(uint seed)
        {
            ulong mixed = seed;
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352DUL;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68BUL;
            mixed ^= mixed >> 16;
            mixed = (mixed << 32) | (mixed ^ 0xA5A5A5A5UL);
            return mixed == 0UL ? ZeroStateFallback : mixed;
        }

        private static ulong ToStateValue(RngState state)
        {
            ulong value = ((ulong)state.S0 << 32) | state.S1;
            return value == 0UL ? ZeroStateFallback : value;
        }

        private static RngState FromStateValue(ulong state)
        {
            return new RngState((uint)(state >> 32), (uint)state);
        }

        private void SetState(ulong state)
        {
            RngState rngState = FromStateValue(state == 0UL ? ZeroStateFallback : state);
            _s0 = rngState.S0;
            _s1 = rngState.S1;
        }

        private ulong NextUInt64()
        {
            ulong state = ((ulong)_s0 << 32) | _s1;
            if (state == 0UL)
            {
                state = ZeroStateFallback;
            }

            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;

            SetState(state);
            return state * 2685821657736338717UL;
        }

        private double NextUnitDouble()
        {
            return (NextUInt64() >> 11) * (1.0d / (1UL << 53));
        }
    }
}
