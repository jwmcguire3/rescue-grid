using System;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Unity.BoardPresentation
{
    public readonly record struct WaterRowResolution(
        ImmutableArray<int> FloodedRowIndices,
        int ForecastRowIndex,
        bool HasForecastRow,
        float NormalizedCounterProgress);

    public static class WaterRowResolver
    {
        public static WaterRowResolution Resolve(int boardHeight, WaterState water)
        {
            if (boardHeight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardHeight));
            }

            if (water is null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            int floodedRowCount = Math.Clamp(water.FloodedRows, 0, boardHeight);
            ImmutableArray<int>.Builder floodedRows = ImmutableArray.CreateBuilder<int>(floodedRowCount);
            for (int row = boardHeight - floodedRowCount; row < boardHeight; row++)
            {
                floodedRows.Add(row);
            }

            int forecastRowIndex = boardHeight - floodedRowCount - 1;
            bool hasForecastRow = forecastRowIndex >= 0 && forecastRowIndex < boardHeight;

            return new WaterRowResolution(
                floodedRows.ToImmutable(),
                hasForecastRow ? forecastRowIndex : -1,
                hasForecastRow,
                ResolveNormalizedCounterProgress(water));
        }

        private static float ResolveNormalizedCounterProgress(WaterState water)
        {
            if (water.PauseUntilFirstAction || water.RiseInterval <= 0)
            {
                return 0f;
            }

            int elapsedActions = water.RiseInterval - water.ActionsUntilRise;
            float normalized = elapsedActions / (float)water.RiseInterval;
            return Math.Clamp(normalized, 0f, 1f);
        }
    }
}
