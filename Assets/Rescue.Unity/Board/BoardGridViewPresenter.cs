using System.Collections.Generic;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    public sealed class BoardGridViewPresenter : MonoBehaviour
    {
        private const string DefaultBoardRootName = "BoardGrid";

        [SerializeField] private Transform? boardRoot;
        [SerializeField] private TileVisualRegistry? tileRegistry;
        [SerializeField] private GameObject? dryTilePrefab;
        [SerializeField] private GameObject? fallbackTilePrefab;
        [SerializeField] private float cellSize = 1.0f;
        [SerializeField] private bool centerBoard = true;
        [SerializeField] private Vector3 tileRotationOffset;
        [SerializeField] private Vector3 tileScaleMultiplier = Vector3.one;

        private readonly Dictionary<TileCoord, Transform> cellAnchors = new Dictionary<TileCoord, Transform>();

        public readonly record struct RowWorldBounds(
            Vector3 Left,
            Vector3 Right,
            Quaternion Rotation,
            float CellWidth,
            float Depth)
        {
            public Vector3 Center => (Left + Right) * 0.5f;

            public float Width => Mathf.Max(1f, Vector3.Distance(Left, Right) + CellWidth);
        }

        public void RebuildGrid(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(BoardGridViewPresenter)} requires a valid GameState to rebuild.", this);
                return;
            }

            ClearGrid();

            Transform root = ResolveBoardRoot();
            GameObject? tilePrefab = ResolveTilePrefab();
            Vector3 originOffset = centerBoard ? CalculateCenteredOffset(state.Board.Width, state.Board.Height) : Vector3.zero;

            for (int row = 0; row < state.Board.Height; row++)
            {
                for (int col = 0; col < state.Board.Width; col++)
                {
                    TileCoord coord = new TileCoord(row, col);
                    GameObject anchorObject = new GameObject($"Cell_{row:00}_{col:00}");
                    BoardCellView anchorCellView = anchorObject.AddComponent<BoardCellView>();
                    BoxCollider anchorCollider = anchorObject.AddComponent<BoxCollider>();
                    anchorCellView.Initialize(coord);
                    anchorCollider.size = new Vector3(cellSize, 1f, cellSize);
                    anchorCollider.center = Vector3.zero;
                    Transform anchor = anchorObject.transform;
                    anchor.SetParent(root, false);
                    anchor.localPosition = originOffset + new Vector3(col * cellSize, 0f, -row * cellSize);
                    anchor.localRotation = Quaternion.identity;
                    anchor.localScale = Vector3.one;

                    cellAnchors.Add(coord, anchor);

                    if (tilePrefab is null)
                    {
                        continue;
                    }

                    GameObject tileObject = Instantiate(tilePrefab, anchor);
                    tileObject.name = $"Tile_{row:00}_{col:00}";
                    BoardCellView tileCellView = tileObject.GetComponent<BoardCellView>() ?? tileObject.AddComponent<BoardCellView>();
                    tileCellView.Initialize(coord);

                    Transform tileTransform = tileObject.transform;
                    tileTransform.localPosition = Vector3.zero;
                    tileTransform.localRotation = Quaternion.Euler(tileRotationOffset);
                    tileTransform.localScale = Vector3.Scale(tilePrefab.transform.localScale, tileScaleMultiplier);
                }
            }
        }

        public void ClearGrid()
        {
            foreach (KeyValuePair<TileCoord, Transform> entry in cellAnchors)
            {
                Transform? anchor = entry.Value;
                if (anchor is null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(anchor.gameObject);
                }
                else
                {
                    DestroyImmediate(anchor.gameObject);
                }
            }

            cellAnchors.Clear();
        }

        public bool TryGetCellAnchor(TileCoord coord, out Transform anchor)
        {
            return cellAnchors.TryGetValue(coord, out anchor);
        }

        public bool IsCoordVisible(TileCoord coord)
        {
            return cellAnchors.ContainsKey(coord);
        }

        public bool TryGetCellWorldPosition(TileCoord coord, out Vector3 position)
        {
            if (TryGetCellAnchor(coord, out Transform anchor))
            {
                position = anchor.position;
                return true;
            }

            position = transform.position;
            return false;
        }

        public Vector3 GetCellWorldPosition(TileCoord coord)
        {
            return TryGetCellWorldPosition(coord, out Vector3 position) ? position : transform.position;
        }

        public Vector3 GetColumnEntryWorldPosition(int column)
        {
            if (cellAnchors.Count == 0)
            {
                return transform.position + (Vector3.up * cellSize);
            }

            int topRow = int.MaxValue;
            bool found = false;
            foreach (KeyValuePair<TileCoord, Transform> entry in cellAnchors)
            {
                if (entry.Key.Col != column || entry.Value is null)
                {
                    continue;
                }

                if (entry.Key.Row < topRow)
                {
                    topRow = entry.Key.Row;
                    found = true;
                }
            }

            if (!found)
            {
                return transform.position + (Vector3.up * cellSize);
            }

            return GetCellWorldPosition(new TileCoord(topRow, column)) + (Vector3.up * cellSize);
        }

        public bool TryGetRowWorldBounds(int row, out RowWorldBounds bounds)
        {
            bounds = default;

            Transform? leftAnchor = null;
            Transform? rightAnchor = null;
            int minCol = int.MaxValue;
            int maxCol = int.MinValue;

            foreach (KeyValuePair<TileCoord, Transform> entry in cellAnchors)
            {
                if (entry.Key.Row != row || entry.Value is null)
                {
                    continue;
                }

                if (entry.Key.Col < minCol)
                {
                    minCol = entry.Key.Col;
                    leftAnchor = entry.Value;
                }

                if (entry.Key.Col > maxCol)
                {
                    maxCol = entry.Key.Col;
                    rightAnchor = entry.Value;
                }
            }

            if (leftAnchor is null || rightAnchor is null)
            {
                return false;
            }

            float cellWidth = ResolveRowCellWidth(row, minCol, maxCol, leftAnchor, rightAnchor);
            float depth = ResolveRowDepth(row, minCol);
            bounds = new RowWorldBounds(leftAnchor.position, rightAnchor.position, leftAnchor.rotation, cellWidth, depth);
            return true;
        }

        private Vector3 CalculateCenteredOffset(int width, int height)
        {
            float xOffset = -((width - 1) * cellSize * 0.5f);
            float zOffset = (height - 1) * cellSize * 0.5f;
            return new Vector3(xOffset, 0f, zOffset);
        }

        private float ResolveRowCellWidth(int row, int minCol, int maxCol, Transform leftAnchor, Transform rightAnchor)
        {
            if (maxCol > minCol)
            {
                return Vector3.Distance(leftAnchor.position, rightAnchor.position) / (maxCol - minCol);
            }

            if (TryGetCellWorldPosition(new TileCoord(row, minCol + 1), out Vector3 adjacentPosition))
            {
                return Vector3.Distance(leftAnchor.position, adjacentPosition);
            }

            return 1f;
        }

        private float ResolveRowDepth(int row, int referenceCol)
        {
            if (!TryGetCellWorldPosition(new TileCoord(row, referenceCol), out Vector3 currentPosition))
            {
                return 1f;
            }

            if (TryGetCellWorldPosition(new TileCoord(row - 1, referenceCol), out Vector3 abovePosition))
            {
                return abovePosition.z - currentPosition.z;
            }

            if (TryGetCellWorldPosition(new TileCoord(row + 1, referenceCol), out Vector3 belowPosition))
            {
                return currentPosition.z - belowPosition.z;
            }

            return 1f;
        }

        private Transform ResolveBoardRoot()
        {
            if (boardRoot is not null)
            {
                return boardRoot;
            }

            Transform existingRoot = transform.Find(DefaultBoardRootName);
            if (existingRoot is not null)
            {
                boardRoot = existingRoot;
                return existingRoot;
            }

            GameObject rootObject = new GameObject(DefaultBoardRootName);
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(transform, false);
            boardRoot = rootTransform;

            Debug.LogWarning(
                $"{nameof(BoardGridViewPresenter)} is missing {nameof(boardRoot)}. Created a fallback '{DefaultBoardRootName}' container.",
                this);

            return rootTransform;
        }

        private GameObject? ResolveTilePrefab()
        {
            GameObject? registryPrefab = tileRegistry?.GetDryTilePrefab();
            if (registryPrefab is not null)
            {
                return registryPrefab;
            }

            if (dryTilePrefab is not null)
            {
                return dryTilePrefab;
            }

            if (fallbackTilePrefab is not null)
            {
                return fallbackTilePrefab;
            }

            Debug.LogWarning(
                $"{nameof(BoardGridViewPresenter)} is missing both {nameof(dryTilePrefab)} and {nameof(fallbackTilePrefab)}.",
                this);
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncFallbackTilePrefab();
        }

        private void Reset()
        {
            SyncFallbackTilePrefab();
        }

        private void SyncFallbackTilePrefab()
        {
            if (fallbackTilePrefab is null && dryTilePrefab is not null)
            {
                fallbackTilePrefab = dryTilePrefab;
            }
        }
#endif
    }
}
