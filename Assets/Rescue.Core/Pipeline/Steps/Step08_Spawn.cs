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
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            ImmutableArray<SpawnedPiece>.Builder spawnedPieces = ImmutableArray.CreateBuilder<SpawnedPiece>(spawnCoords.Length);

            if (state.DebugSpawnOverride is not null)
            {
                SpawnBias overrideBias = SpawnOps.ComputeSpawnBias(state, state.LevelConfig, state.DebugSpawnOverride, spawnCoord: null);
                events.Add(new DebugSpawnOverrideApplied(
                    state.DebugSpawnOverride,
                    SpawnOps.IsEmergencyRequested(state, state.DebugSpawnOverride),
                    overrideBias.IsEmergency,
                    overrideBias.EffectiveAssistanceChance));
            }

            for (int i = 0; i < spawnCoords.Length; i++)
            {
                TileCoord coord = spawnCoords[i];
                bool wasSingletonOnly = SpawnOps.BoardIsSingletonOnly(updatedState.Board);
                bool usedRecoveryBias = updatedState.SpawnRecoveryCounter > 0;
                bool emergencyActive = SpawnOps.IsEmergencyActive(updatedState, updatedState.LevelConfig, updatedState.DebugSpawnOverride);
                DebrisType debrisType = SpawnOps.ChooseNextSpawn(updatedState, coord, rng);
                int lineageId = updatedState.NextSpawnLineageId;
                SpawnedPiece spawnedPiece = SpawnOps.DescribeSpawnedPiece(updatedState, coord, debrisType, lineageId);

                Board boardWithSpawn = BoardHelpers.SetTile(updatedState.Board, coord, new DebrisTile(debrisType));
                ImmutableDictionary<TileCoord, SpawnLineage> lineageByCoord = updatedState.SpawnLineageByCoord.SetItem(
                    coord,
                    new SpawnLineage(lineageId, debrisType, coord));
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
                    SpawnLineageByCoord = lineageByCoord,
                    NextSpawnLineageId = lineageId + 1,
                    RngState = rng.GetState(),
                    ConsecutiveEmergencySpawns = emergencyActive
                        ? updatedState.ConsecutiveEmergencySpawns + 1
                        : 0,
                    SpawnRecoveryCounter = recoveryCounter,
                };

                // TODO(B4.5 follow-up): update LastRouteBoostedType/ConsecutiveRouteBoosts here if safeguard state is accepted into scope.

                spawnedPieces.Add(spawnedPiece);
            }

            events.Add(new Spawned(spawnedPieces.ToImmutable()));
            return new StepResult(updatedState, context, events.ToImmutable());
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
