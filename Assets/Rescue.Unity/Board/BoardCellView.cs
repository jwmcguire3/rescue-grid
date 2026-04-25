using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    public sealed class BoardCellView : MonoBehaviour
    {
        [SerializeField] private int row;
        [SerializeField] private int col;

        public TileCoord Coord => new TileCoord(row, col);

        public void Initialize(TileCoord coord)
        {
            row = coord.Row;
            col = coord.Col;
        }
    }
}
