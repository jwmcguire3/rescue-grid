using System;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Unity.BoardPresentation
{
    public readonly record struct WaterFeedbackResolution(
        bool HasWaterRise,
        ImmutableArray<int> NewlyFloodedRowIndices,
        bool HasNearRiseWarning,
        bool ShouldPulseWaterline,
        bool ShouldEmphasizeCounter);

    public static class WaterFeedbackResolver
    {
        private const int NearRiseThreshold = 1;

        public static WaterFeedbackResolution Resolve(int boardHeight, WaterState? previous, WaterState current)
        {
            if (boardHeight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardHeight));
            }

            if (current is null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            int previousFloodedRows = Math.Clamp(previous?.FloodedRows ?? current.FloodedRows, 0, boardHeight);
            int currentFloodedRows = Math.Clamp(current.FloodedRows, 0, boardHeight);
            bool hasWaterRise = currentFloodedRows > previousFloodedRows;

            ImmutableArray<int>.Builder newlyFloodedRows = ImmutableArray.CreateBuilder<int>(
                hasWaterRise ? currentFloodedRows - previousFloodedRows : 0);

            if (hasWaterRise)
            {
                for (int row = boardHeight - currentFloodedRows; row < boardHeight - previousFloodedRows; row++)
                {
                    newlyFloodedRows.Add(row);
                }
            }

            bool hasNearRiseWarning = !current.PauseUntilFirstAction
                && currentFloodedRows < boardHeight
                && current.ActionsUntilRise <= NearRiseThreshold;

            bool shouldEmphasizeCounter = previous is not null
                && (previous.ActionsUntilRise != current.ActionsUntilRise
                    || previous.RiseInterval != current.RiseInterval
                    || previous.PauseUntilFirstAction != current.PauseUntilFirstAction);

            return new WaterFeedbackResolution(
                HasWaterRise: hasWaterRise,
                NewlyFloodedRowIndices: newlyFloodedRows.ToImmutable(),
                HasNearRiseWarning: hasNearRiseWarning,
                ShouldPulseWaterline: hasNearRiseWarning,
                ShouldEmphasizeCounter: shouldEmphasizeCounter);
        }
    }
}
