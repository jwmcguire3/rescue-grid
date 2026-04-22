namespace Rescue.Core.State
{
    public static class WaterHelpers
    {
        public static int? GetNextFloodRow(Board board, WaterState water)
        {
            if (water.FloodedRows >= board.Height)
            {
                return null;
            }

            return board.Height - water.FloodedRows - 1;
        }

        public static bool HasForecast(Board board, WaterState water)
        {
            return GetNextFloodRow(board, water).HasValue;
        }
    }
}
