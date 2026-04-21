using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;

namespace Rescue.Content.Tests
{
    public sealed class LoaderTests
    {
        [Test]
        public void LoadLevel_BuildsExpectedMinimalBoard()
        {
            LevelJson level = TestLevels.MinimalLevel();

            GameState state = Loader.LoadLevel(level, seed: 17);

            Assert.That(state.Board.Width, Is.EqualTo(3));
            Assert.That(state.Board.Height, Is.EqualTo(3));
            Assert.That(BoardHelpers.GetTile(state.Board, new TileCoord(0, 0)), Is.TypeOf<DebrisTile>());
            Assert.That(BoardHelpers.GetTile(state.Board, new TileCoord(0, 1)), Is.TypeOf<DebrisTile>());
            Assert.That(BoardHelpers.GetTile(state.Board, new TileCoord(1, 1)), Is.EqualTo(new BlockerTile(BlockerType.Crate, 1, Hidden: null)));
            Assert.That(BoardHelpers.GetTile(state.Board, new TileCoord(2, 1)), Is.EqualTo(new TargetTile("0", Extracted: false)));
            Assert.That(state.Targets.Length, Is.EqualTo(1));
            Assert.That(state.Targets[0], Is.EqualTo(new TargetState("0", new TileCoord(2, 1), Extracted: false, OneClearAway: true)));
            Assert.That(state.Water.ActionsUntilRise, Is.EqualTo(level.Water.RiseInterval));
            Assert.That(state.Dock.Size, Is.EqualTo(7));
        }

        [Test]
        public void LoadLevel_WithSameSeed_IsDeterministic()
        {
            LevelJson level = TestLevels.MinimalLevel();

            GameState first = Loader.LoadLevel(level, seed: 42);
            GameState second = Loader.LoadLevel(level, seed: 42);

            Assert.That(second.RngState, Is.EqualTo(first.RngState));
            Assert.That(second.Water, Is.EqualTo(first.Water));
            Assert.That(second.Vine, Is.EqualTo(first.Vine));
            Assert.That(second.LevelConfig.AssistanceChance, Is.EqualTo(first.LevelConfig.AssistanceChance));
            Assert.That(second.LevelConfig.ConsecutiveEmergencyCap, Is.EqualTo(first.LevelConfig.ConsecutiveEmergencyCap));
            Assert.That(second.LevelConfig.BaseDistribution, Is.EqualTo(first.LevelConfig.BaseDistribution));
            Assert.That(second.LevelConfig.DebrisTypePool.Length, Is.EqualTo(first.LevelConfig.DebrisTypePool.Length));
            for (int i = 0; i < first.LevelConfig.DebrisTypePool.Length; i++)
            {
                Assert.That(second.LevelConfig.DebrisTypePool[i], Is.EqualTo(first.LevelConfig.DebrisTypePool[i]));
            }
            Assert.That(second.Targets.Length, Is.EqualTo(first.Targets.Length));
            Assert.That(second.Targets[0], Is.EqualTo(first.Targets[0]));
            Assert.That(second.Board.Width, Is.EqualTo(first.Board.Width));
            Assert.That(second.Board.Height, Is.EqualTo(first.Board.Height));
            for (int row = 0; row < first.Board.Height; row++)
            {
                for (int col = 0; col < first.Board.Width; col++)
                {
                    Assert.That(
                        BoardHelpers.GetTile(second.Board, new TileCoord(row, col)),
                        Is.EqualTo(BoardHelpers.GetTile(first.Board, new TileCoord(row, col))));
                }
            }
        }
    }
}
