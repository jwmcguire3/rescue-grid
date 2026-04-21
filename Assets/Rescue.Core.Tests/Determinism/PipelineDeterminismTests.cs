using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Determinism
{
    public sealed class PipelineDeterminismTests
    {
        [Test]
        public void RepeatedRunsProduceIdenticalFinalStateAndEventStream()
        {
            const uint seed = 424242u;
            IReadOnlyList<ActionInput> inputs = CreateInputSequence();

            DeterminismHarness.AssertIdenticalRuns(
                seed,
                inputs,
                stepFn: static (run, input) =>
                {
                    ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(run.State, input);
                    return run with { State = result.State, Events = run.Events.AddRange(result.Events) };
                },
                initialStateFromSeed: CreateInitialRunState);
        }

        private static PipelineRunState CreateInitialRunState(uint seed)
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.B, DebrisType.B),
                    PipelineTestFixtures.DebrisRow(DebrisType.C, DebrisType.C, DebrisType.D, DebrisType.D),
                    PipelineTestFixtures.DebrisRow(DebrisType.A, DebrisType.A, DebrisType.C, DebrisType.C),
                    PipelineTestFixtures.EmptyRow(4)));

            state = state with
            {
                RngState = new Rescue.Core.Rng.RngState(seed, seed ^ 0xA5A5A5A5u),
            };

            return new PipelineRunState(state, ImmutableArray<ActionEvent>.Empty);
        }

        private static IReadOnlyList<ActionInput> CreateInputSequence()
        {
            TileCoord[] taps =
            {
                new TileCoord(0, 0),
                new TileCoord(0, 2),
                new TileCoord(1, 0),
                new TileCoord(1, 2),
                new TileCoord(2, 0),
                new TileCoord(2, 2),
                new TileCoord(3, 0),
                new TileCoord(0, 0),
                new TileCoord(0, 1),
                new TileCoord(0, 2),
                new TileCoord(1, 0),
                new TileCoord(1, 1),
                new TileCoord(1, 2),
                new TileCoord(2, 0),
                new TileCoord(2, 1),
                new TileCoord(2, 2),
                new TileCoord(3, 1),
                new TileCoord(3, 2),
                new TileCoord(3, 3),
                new TileCoord(0, 3),
            };

            List<ActionInput> inputs = new List<ActionInput>(taps.Length);
            for (int i = 0; i < taps.Length; i++)
            {
                inputs.Add(new ActionInput(taps[i]));
            }

            return inputs;
        }

        private sealed record PipelineRunState(GameState State, ImmutableArray<ActionEvent> Events) : IEquatable<PipelineRunState>;
    }
}
