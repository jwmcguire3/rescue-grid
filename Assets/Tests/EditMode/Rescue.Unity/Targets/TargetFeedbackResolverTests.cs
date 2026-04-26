using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;

namespace Rescue.Unity.Targets.Tests
{
    public sealed class TargetFeedbackResolverTests
    {
        [Test]
        public void TargetFeedbackResolver_DetectsBecameOneClearAway()
        {
            TargetFeedbackResolution resolution = TargetFeedbackResolver.Resolve(
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: false)),
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: true)));

            Assert.That(resolution.Events, Is.EqualTo(new[]
            {
                new TargetFeedbackEvent("pup-1", new TileCoord(2, 1), TargetFeedbackKind.NearRescue),
            }));
        }

        [Test]
        public void TargetFeedbackResolver_DoesNotRepeatOneClearAwayIfAlreadyTrue()
        {
            TargetFeedbackResolution resolution = TargetFeedbackResolver.Resolve(
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: true)),
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: true)));

            Assert.That(resolution.Events, Is.Empty);
        }

        [Test]
        public void TargetFeedbackResolver_DetectsExtraction()
        {
            TargetFeedbackResolution resolution = TargetFeedbackResolver.Resolve(
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: true)),
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: true, OneClearAway: true)));

            Assert.That(resolution.Events, Is.EqualTo(new[]
            {
                new TargetFeedbackEvent("pup-1", new TileCoord(2, 1), TargetFeedbackKind.Extraction),
            }));
        }

        [Test]
        public void TargetFeedbackResolver_IgnoresAlreadyExtractedTarget()
        {
            TargetFeedbackResolution resolution = TargetFeedbackResolver.Resolve(
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: true, OneClearAway: true)),
                CreateState(new TargetState("pup-1", new TileCoord(2, 1), Extracted: true, OneClearAway: true)));

            Assert.That(resolution.Events, Is.Empty);
        }

        private static GameState CreateState(TargetState target)
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new TargetTile(target.TargetId, target.Extracted),
                    new EmptyTile()));

            return new GameState(
                Board: new Board(3, 3, rows),
                Dock: new Dock(ImmutableArray<DebrisType?>.Empty, Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(target),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    BaseDistribution: null,
                    AssistanceChance: 0.0d,
                    ConsecutiveEmergencyCap: 2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }
    }
}
