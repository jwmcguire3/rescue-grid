using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Rng;
using Rescue.Core.State;

namespace Rescue.Core.Tests.State
{
    public sealed class ImmutabilityTests
    {
        [Test]
        public void RecordWithExpressionsLeaveOriginalInstancesUnchanged()
        {
            TileCoord coord = new TileCoord(1, 2);
            DebrisTile hiddenDebris = new DebrisTile(DebrisType.C);
            BlockerTile blocker = new BlockerTile(BlockerType.Ice, 1, hiddenDebris);
            TargetTile targetTile = new TargetTile("target-1", Extracted: false);
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(
                    blocker,
                    targetTile),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new FloodedTile()));
            Dock dock = new Dock(
                ImmutableArray.Create<DebrisType?>(
                    DebrisType.A,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                Size: 7);
            WaterState water = new WaterState(FloodedRows: 1, ActionsUntilRise: 2, RiseInterval: 3);
            VineState vine = new VineState(
                ActionsSinceLastClear: 1,
                GrowthThreshold: 4,
                GrowthPriorityList: ImmutableArray.Create(new TileCoord(2, 2)),
                PriorityCursor: 0,
                PendingGrowthTile: new TileCoord(3, 3));
            TargetState targetState = new TargetState("target-1", new TileCoord(0, 1), Extracted: false, OneClearAway: false);
            GameState gameState = new GameState(
                Board: board,
                Dock: dock,
                Water: water,
                Vine: vine,
                Targets: ImmutableArray.Create(targetState),
                LevelConfig: Rescue.Core.Tests.Pipeline.PipelineTestFixtures.CreateLevelConfig(),
                RngState: new RngState(10U, 20U),
                ActionCount: 5,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray.Create("target-0"),
                Frozen: false,
                ConsecutiveEmergencySpawns: 1,
                SpawnRecoveryCounter: 2,
                DockJamEnabled: true,
                DockJamActive: false);

            TileCoord updatedCoord = coord with { Row = 9 };
            DebrisTile updatedHidden = hiddenDebris with { Type = DebrisType.E };
            BlockerTile updatedBlocker = blocker with { Hp = 0, Hidden = updatedHidden };
            TargetTile updatedTargetTile = targetTile with { Extracted = true };
            Board updatedBoard = board with { Width = 9 };
            Dock updatedDock = dock with { Size = 5 };
            WaterState updatedWater = water with { ActionsUntilRise = 1 };
            VineState updatedVine = vine with { PriorityCursor = 1 };
            TargetState updatedTargetState = targetState with { OneClearAway = true };
            GameState updatedGameState = gameState with { Frozen = true, ActionCount = 6 };

            Assert.That(coord, Is.EqualTo(new TileCoord(1, 2)));
            Assert.That(updatedCoord, Is.EqualTo(new TileCoord(9, 2)));
            Assert.That(blocker, Is.EqualTo(new BlockerTile(BlockerType.Ice, 1, new DebrisTile(DebrisType.C))));
            Assert.That(updatedBlocker, Is.EqualTo(new BlockerTile(BlockerType.Ice, 0, new DebrisTile(DebrisType.E))));
            Assert.That(targetTile, Is.EqualTo(new TargetTile("target-1", false)));
            Assert.That(updatedTargetTile, Is.EqualTo(new TargetTile("target-1", true)));
            Assert.That(board.Width, Is.EqualTo(2));
            Assert.That(updatedBoard.Width, Is.EqualTo(9));
            Assert.That(dock.Size, Is.EqualTo(7));
            Assert.That(updatedDock.Size, Is.EqualTo(5));
            Assert.That(water.ActionsUntilRise, Is.EqualTo(2));
            Assert.That(updatedWater.ActionsUntilRise, Is.EqualTo(1));
            Assert.That(vine.PriorityCursor, Is.EqualTo(0));
            Assert.That(updatedVine.PriorityCursor, Is.EqualTo(1));
            Assert.That(targetState.OneClearAway, Is.False);
            Assert.That(updatedTargetState.OneClearAway, Is.True);
            Assert.That(gameState.Frozen, Is.False);
            Assert.That(gameState.ActionCount, Is.EqualTo(5));
            Assert.That(updatedGameState.Frozen, Is.True);
            Assert.That(updatedGameState.ActionCount, Is.EqualTo(6));
        }

        [Test]
        public void SetTileLeavesInputBoardUnchanged()
        {
            Board original = CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new EmptyTile()));

            Board updated = BoardHelpers.SetTile(original, new TileCoord(0, 0), new FloodedTile());

            Assert.That(BoardHelpers.GetTile(original, new TileCoord(0, 0)), Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(BoardHelpers.GetTile(updated, new TileCoord(0, 0)), Is.EqualTo(new FloodedTile()));
        }

        [Test]
        public void ImmutableArrayPropertiesExposeImmutableSnapshots()
        {
            Board board = CreateBoard(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B)));
            Dock dock = new Dock(
                ImmutableArray.Create<DebrisType?>(
                    DebrisType.A,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                Size: 7);
            VineState vine = new VineState(
                ActionsSinceLastClear: 0,
                GrowthThreshold: 4,
                GrowthPriorityList: ImmutableArray.Create(new TileCoord(0, 0)),
                PriorityCursor: 0,
                PendingGrowthTile: null);
            GameState gameState = new GameState(
                Board: board,
                Dock: dock,
                Water: new WaterState(0, 3, 3),
                Vine: vine,
                Targets: ImmutableArray.Create(new TargetState("target-1", new TileCoord(0, 0), false, true)),
                LevelConfig: Rescue.Core.Tests.Pipeline.PipelineTestFixtures.CreateLevelConfig(),
                RngState: new RngState(1U, 2U),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: false,
                ExtractedTargetOrder: ImmutableArray.Create("target-0"),
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0,
                DockJamEnabled: false,
                DockJamActive: false);

            // The following operations are the intended mutation pattern: they produce new immutable snapshots.
            ImmutableArray<ImmutableArray<Tile>> updatedTiles = board.Tiles.SetItem(0, board.Tiles[0].SetItem(0, new EmptyTile()));
            ImmutableArray<DebrisType?> updatedSlots = dock.Slots.SetItem(1, DebrisType.B);
            ImmutableArray<TileCoord> updatedGrowthList = vine.GrowthPriorityList.Add(new TileCoord(1, 1));
            ImmutableArray<TargetState> updatedTargets = gameState.Targets.SetItem(0, gameState.Targets[0] with { Extracted = true });
            ImmutableArray<string> updatedOrder = gameState.ExtractedTargetOrder.Add("target-1");

            Assert.That(board.Tiles[0][0], Is.EqualTo(new DebrisTile(DebrisType.A)));
            Assert.That(updatedTiles[0][0], Is.EqualTo(new EmptyTile()));
            Assert.That(dock.Slots[1], Is.Null);
            Assert.That(updatedSlots[1], Is.EqualTo(DebrisType.B));
            Assert.That(vine.GrowthPriorityList.Length, Is.EqualTo(1));
            Assert.That(updatedGrowthList.Length, Is.EqualTo(2));
            Assert.That(gameState.Targets[0].Extracted, Is.False);
            Assert.That(updatedTargets[0].Extracted, Is.True);
            Assert.That(gameState.ExtractedTargetOrder.Length, Is.EqualTo(1));
            Assert.That(updatedOrder.Length, Is.EqualTo(2));
        }

        private static Board CreateBoard(params ImmutableArray<Tile>[] rows)
        {
            return new Board(rows[0].Length, rows.Length, rows.ToImmutableArray());
        }
    }
}
