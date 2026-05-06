using System.Collections.Generic;
using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    internal sealed class BoardPieceView
    {
        public BoardPieceView(
            TileCoord coord,
            string contentLabel,
            GameObject contentObject,
            Vector3 baseLocalScale,
            Quaternion baseLocalRotation)
        {
            Coord = coord;
            ContentLabel = contentLabel;
            Object = contentObject;
            BaseLocalScale = baseLocalScale;
            BaseLocalRotation = baseLocalRotation;
        }

        public TileCoord Coord { get; set; }

        public string ContentLabel { get; set; }

        public GameObject Object { get; }

        public Vector3 BaseLocalScale { get; set; }

        public Quaternion BaseLocalRotation { get; set; }
    }

    internal sealed class TargetVisualView
    {
        public TargetVisualView(GameObject contentObject)
        {
            Object = contentObject;
        }

        public bool IsExtracting { get; set; }

        public GameObject Object { get; }
    }

    internal sealed class BoardPieceRegistry
    {
        private readonly Dictionary<TileCoord, BoardPieceView> viewsByCoord = new Dictionary<TileCoord, BoardPieceView>();

        public bool TryGet(TileCoord coord, out BoardPieceView? view)
        {
            if (viewsByCoord.TryGetValue(coord, out BoardPieceView existingView))
            {
                view = existingView;
                return true;
            }

            view = null;
            return false;
        }

        public bool Contains(TileCoord coord)
        {
            return viewsByCoord.ContainsKey(coord);
        }

        public void Set(TileCoord coord, BoardPieceView view)
        {
            viewsByCoord[coord] = view;
        }

        public bool Remove(TileCoord coord)
        {
            return viewsByCoord.Remove(coord);
        }

        public bool Remove(TileCoord coord, out BoardPieceView? view)
        {
            if (viewsByCoord.TryGetValue(coord, out BoardPieceView existingView))
            {
                viewsByCoord.Remove(coord);
                view = existingView;
                return true;
            }

            view = null;
            return false;
        }

        public List<TileCoord> GetCoordsSnapshot()
        {
            return new List<TileCoord>(viewsByCoord.Keys);
        }

        public void AddTrackedObjects(ISet<GameObject> trackedObjects)
        {
            foreach (BoardPieceView view in viewsByCoord.Values)
            {
                if (view.Object != null)
                {
                    trackedObjects.Add(view.Object);
                }
            }
        }

        public void Clear()
        {
            viewsByCoord.Clear();
        }
    }

    internal sealed class BoardContentVisualRegistry
    {
        public BoardPieceRegistry Debris { get; } = new BoardPieceRegistry();

        public BoardPieceRegistry Blockers { get; } = new BoardPieceRegistry();

        public BoardPieceRegistry HiddenDebris { get; } = new BoardPieceRegistry();

        public BoardPieceRegistry RescuePath { get; } = new BoardPieceRegistry();

        public void AddTrackedObjects(ISet<GameObject> trackedObjects)
        {
            Debris.AddTrackedObjects(trackedObjects);
            Blockers.AddTrackedObjects(trackedObjects);
            HiddenDebris.AddTrackedObjects(trackedObjects);
            RescuePath.AddTrackedObjects(trackedObjects);
        }

        public void Clear()
        {
            Debris.Clear();
            Blockers.Clear();
            HiddenDebris.Clear();
            RescuePath.Clear();
        }
    }
}
