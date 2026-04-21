using System.Collections.Immutable;
using Rescue.Core.Rng;
using Rescue.Core.Rules;
using Rescue.Core.State;

namespace Rescue.Core.Pipeline.Steps
{
    internal static class Step08_Spawn
    {
        public static StepResult Run(GameState state, StepContext context)
        {
            ImmutableArray<TileCoord> spawnCoords = FindSpawnCoords(state.Board, state.Water);
            if (spawnCoords.IsDefaultOrEmpty)
            {
                return new StepResult(state, context, ImmutableArray<ActionEvent>.Empty);
            }

            SeededRng rng = SeededRng.FromState(state.RngState);
            GameState updatedState = state;
            ImmutableArray<(TileCoord Coord, DebrisType Type)>.Builder spawnedPieces = ImmutableArray.CreateBuilder<(TileCoord Coord, DebrisType Type)>(spawnCoords.Length);

            for (int i = 0; i < spawnCoords.Length; i++)
            {
                TileCoord coord = spawnCoords[i];
                bool wasSingletonOnly = SpawnOps.BoardIsSingletonOnly(updatedState.Board);
                bool usedRecoveryBias = updatedState.SpawnRecoveryCounter > 0;
                bool emergencyActive = SpawnOps.IsEmergencyActive(updatedState, updatedState.LevelConfig);
                DebrisType debrisType = SpawnOps.ChooseNextSpawn(updatedState, coord, rng);

                Board boardWithSpawn = BoardHelpers.SetTile(updatedState.Board, coord, new DebrisTile(debrisType));
                int recoveryCounter = usedRecoveryBias
                    ? updatedState.SpawnRecoveryCounter - 1
                    : updatedState.SpawnRecoveryCounter;

                if (!wasSingletonOnly && SpawnOps.BoardIsSingletonOnly(boardWithSpawn))
                {
                    recoveryCounter = 2;
                }

                updatedState = updatedState with
                {
                    Board = boardWithSpawn,
                    RngState = rng.GetState(),
                    ConsecutiveEmergencySpawns = emergencyActive
                        ? updatedState.ConsecutiveEmergencySpawns + 1
                        : 0,
                    SpawnRecoveryCounter = recoveryCounter,
                };

                // TODO(B4.5 follow-up): update LastRouteBoostedType/ConsecutiveRouteBoosts here if safeguard state is accepted into scope.

                spawnedPieces.Add((coord, debrisType));
            }

            ImmutableArray<ActionEvent> events = ImmutableArray.Create<ActionEvent>(new Spawned(spawnedPieces.ToImmutable()));
            return new StepResult(updatedState, context, events);
        }

        private static ImmutableArray<TileCoord> FindSpawnCoords(Board board, WaterState water)
        {
            int dryHeight = board.Height - water.FloodedRows;
            ImmutableArray<TileCoord>.Builder coords = ImmutableArray.CreateBuilder<TileCoord>();

            for (int col = 0; col < board.Width; col++)
            {
                for (int row = 0; row < dryHeight; row++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    if (BoardHelpers.GetTile(board, coord) is EmptyTile)
                    {
                        coords.Add(coord);
                        continue;
                    }

                    break;
                }
            }

            return coords.ToImmutable();
        }
    }
}
